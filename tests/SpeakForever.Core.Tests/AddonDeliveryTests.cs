using System.Text.Json;
using SpeakForever.Dictation;
using SpeakForever.Routing;
using VoiceRouter.Core;

namespace SpeakForever.Core.Tests;

public class AddonDeliveryTests
{
    public sealed record Vector(string Text,string Hint,bool Send,string Nonce,string Packet);
    static readonly JsonSerializerOptions JsonOptions=new(){PropertyNameCaseInsensitive=true};

    [Fact]
    public void CompanionPacketsMatchActualLuaAcceptedFixtures()
    {
        var vectors=JsonSerializer.Deserialize<Vector[]>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"addon-envelope.json")),JsonOptions)!;
        foreach(var v in vectors) Assert.Equal(v.Packet,AddonEnvelope.Encode(v.Text,v.Hint,v.Send,v.Nonce));
    }

    [Theory]
    [InlineData("", "default")]
    [InlineData("/logout", "default")]
    [InlineData("hello\nworld", "default")]
    [InlineData("hello", "m:channel:0")]
    [InlineData("hello", "m:run")]
    public void InvalidPayloadCannotBecomeTransport(string text,string hint) =>
        Assert.Throws<ArgumentException>(()=>AddonEnvelope.Encode(text,hint,false,new string('a',32)));

    [Fact]
    public void UnicodeByteLimitRequiresEditing()
    {
        Assert.Null(AddonEnvelope.ValidateMessage(new string('é',100)));
        Assert.NotNull(AddonEnvelope.ValidateMessage(new string('é',101)));
    }

    [Fact]
    public void DefaultRouterNeedsNoCaptureContextAndCopiesHumanText()
    {
        using var router=new DraftRouter(Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")));
        Assert.True(router.UsesAddonRouting);
        router.RefreshContext(); // Must be a no-op; no screenshot or heartbeat is required.
        var draft=router.Prepare("In General, hello friends");
        Assert.True(draft.Ready);
        Assert.Equal("In General, hello friends",draft.ClipboardText);
        Assert.Equal("Auto (addon)",draft.Destination);
        Assert.DoesNotContain("stale",draft.Reason,StringComparison.OrdinalIgnoreCase);
        Assert.False(router.Prepare("/run bad()").Ready);
    }

    [Theory]
    [InlineData("Hey guildmates, hello","hey guildmates, hello addr_guild")]
    [InlineData("I mentioned guildmates","i mentioned guildmates")]
    [InlineData("HELLO\nWorld","hello world")]
    public void IntentFeaturesContainNoAssumedGameContext(string text,string expected) => Assert.Equal(expected,Features.MessageOnly(text));

    [Theory]
    [InlineData("Hello friends",false)]
    [InlineData("I mentioned the guild yesterday",false)]
    [InlineData("Don't tell guild we need help",false)]
    [InlineData("How's the guild doing guys?",true)]
    [InlineData("Guildmates, hello",true)]
    public void GuildSuggestionRequiresAnAudienceAddress(string text,bool expected) => Assert.Equal(expected,Features.HasGuildAddress(text));

    [Fact]
    public async Task FinalClickPreparesInboxThenPastesOnceWithoutEnter()
    {
        var actions=new List<AddonInput>();
        var result=await AddonDeliveryWorkflow.RunAsync(()=>null,action=>{actions.Add(action);return(null,true);},CancellationToken.None,_=>Task.CompletedTask);
        Assert.Null(result.Error);
        Assert.Equal([AddonInput.PrepareInbox,AddonInput.Paste],actions);
    }

    [Fact]
    public async Task ChangedFocusOrClipboardStopsBeforePaste()
    {
        var actions=new List<AddonInput>(); int reads=0;
        var result=await AddonDeliveryWorkflow.RunAsync(()=>++reads==1 ? null : "Clipboard changed",action=>{actions.Add(action);return(null,true);},CancellationToken.None,_=>Task.CompletedTask);
        Assert.Equal("Clipboard changed",result.Error);
        Assert.Equal([AddonInput.PrepareInbox],actions);
    }

    [Fact]
    public async Task CancelAfterPreparingInboxClosesItWithoutPaste()
    {
        using var cts=new CancellationTokenSource();
        var actions=new List<AddonInput>();
        var result=await AddonDeliveryWorkflow.RunAsync(()=>null,action=>{actions.Add(action);return(null,true);},cts.Token,_=>{cts.Cancel();return Task.CompletedTask;});
        Assert.NotNull(result.Error);
        Assert.Equal([AddonInput.PrepareInbox,AddonInput.CancelInbox],actions);
    }

    [Fact]
    public async Task FailedPartialShortcutNeverRetries()
    {
        var actions=new List<AddonInput>();
        var result=await AddonDeliveryWorkflow.RunAsync(()=>null,action=>{actions.Add(action);return("Partial input",true);},CancellationToken.None,_=>Task.CompletedTask);
        Assert.Equal("Partial input",result.Error);
        Assert.Equal([AddonInput.PrepareInbox],actions);
    }
}
