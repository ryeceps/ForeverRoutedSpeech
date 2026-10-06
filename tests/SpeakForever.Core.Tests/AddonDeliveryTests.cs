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

    sealed record ControlVector(string Text,string Hint,string Nonce,string Hex,ushort[] Keys);
    [Fact]
    public void ControlSignalMatchesActualLuaAcceptedVectors()
    {
        var vectors=System.Text.Json.JsonSerializer.Deserialize<ControlVector[]>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"addon-control.json")),JsonOptions)!;
        foreach(var v in vectors)
        {
            Assert.Equal(v.Hex,AddonControl.Encode(v.Text,v.Hint,v.Nonce));
            var keys=AddonControl.Keys(v.Text,v.Hint,v.Nonce);
            Assert.Equal(v.Keys,keys);
            Assert.Equal(AddonControl.BeginKey,keys[0]); Assert.Equal(AddonControl.CommitKey,keys[^1]);
            Assert.All(keys.Skip(1).SkipLast(1),key=>Assert.InRange(key,(ushort)0x7c,(ushort)0x7f));
            Assert.DoesNotContain((ushort)0x0d,keys); Assert.DoesNotContain((ushort)0x1b,keys);
        }
    }

    [Fact]
    public void ProductionWindowsPlanNeverHoldsAltOrUsesCloseKeysAndReleasesEveryKey()
    {
        var held=new HashSet<ushort>();
        var keys=AddonControl.Keys("In General, hello 世界","default",new string('3',32));
        foreach(var stroke in AddonControl.Strokes(keys))
        {
            Assert.True(stroke.Key is 0x10 or 0x11 || stroke.Key is >=0x7c and <=0x82);
            Assert.NotEqual((ushort)0x12,stroke.Key); // Alt must never be synthesized.
            Assert.NotEqual((ushort)0x73,stroke.Key); // F4, including Ctrl+F4, must never occur.
            Assert.NotEqual((ushort)0x0d,stroke.Key);
            Assert.NotEqual((ushort)0x1b,stroke.Key);
            if(stroke.Released) Assert.True(held.Remove(stroke.Key));
            else Assert.True(held.Add(stroke.Key));
        }
        Assert.Empty(held);
        Assert.DoesNotContain(AddonControl.Strokes([AddonControl.CancelKey]),s=>s.Key==0x12);
    }

    [Theory]
    [InlineData((ushort)0x73)] // Retired F4 route, with any modifiers.
    [InlineData((ushort)0x12)] // Alt
    [InlineData((ushort)0x0d)] // Enter
    [InlineData((ushort)0x1b)] // Escape
    [InlineData((ushort)0x5b)] // Windows
    public void UnsafeControlKeyIsRejectedBeforeNativeInput(ushort key) =>
        Assert.Throws<ArgumentException>(()=>AddonControl.Strokes([AddonControl.BeginKey,key,AddonControl.CommitKey]));

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
    public void LegacyAutoSendSettingCannotEnableSubmission() => Assert.False((new SpeakForever.Configuration.Config { AutoSubmit=true }).AutoSubmit);

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
    public async Task NativeOpeningSettlesBeforeFocusSnapshotAndNeverSends()
    {
        var events=new List<string>();
        var result=await AddonDeliveryWorkflow.RunAsync(()=>{events.Add("check");return null;},action=>{events.Add(action.ToString());return(null,true);},CancellationToken.None,_=>{events.Add("settle");return Task.CompletedTask;},nativeChatOpening:true);
        Assert.Null(result.Error);
        Assert.Equal(["settle","check","PrepareControl","settle","check","Paste"],events);
    }

    [Fact]
    public async Task CancelWhileNativeChatOpensPreventsAllInput()
    {
        using var cts=new CancellationTokenSource();
        var actions=new List<AddonInput>();
        var result=await AddonDeliveryWorkflow.RunAsync(()=>null,action=>{actions.Add(action);return(null,true);},cts.Token,_=>{cts.Cancel();return Task.CompletedTask;},nativeChatOpening:true);
        Assert.False(result.Attempted);
        Assert.Empty(actions);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task FinalClickPreparesControlThenPastesOnceWithoutEnter()
    {
        var actions=new List<AddonInput>();
        var result=await AddonDeliveryWorkflow.RunAsync(()=>null,action=>{actions.Add(action);return(null,true);},CancellationToken.None,_=>Task.CompletedTask);
        Assert.Null(result.Error);
        Assert.Equal([AddonInput.PrepareControl,AddonInput.Paste],actions);
    }

    [Fact]
    public async Task ChangedFocusOrClipboardStopsBeforePaste()
    {
        var actions=new List<AddonInput>(); int reads=0;
        var result=await AddonDeliveryWorkflow.RunAsync(()=>++reads==1 ? null : "Clipboard changed",action=>{actions.Add(action);return(null,true);},CancellationToken.None,_=>Task.CompletedTask);
        Assert.Equal("Clipboard changed",result.Error);
        Assert.Equal([AddonInput.PrepareControl],actions);
    }

    [Fact]
    public async Task CancelAfterPreparingControlDisarmsWithoutPaste()
    {
        using var cts=new CancellationTokenSource();
        var actions=new List<AddonInput>();
        var result=await AddonDeliveryWorkflow.RunAsync(()=>null,action=>{actions.Add(action);return(null,true);},cts.Token,_=>{cts.Cancel();return Task.CompletedTask;});
        Assert.NotNull(result.Error);
        Assert.Equal([AddonInput.PrepareControl,AddonInput.CancelControl],actions);
    }

    [Fact]
    public async Task FailedPartialShortcutNeverRetries()
    {
        var actions=new List<AddonInput>();
        var result=await AddonDeliveryWorkflow.RunAsync(()=>null,action=>{actions.Add(action);return("Partial input",true);},CancellationToken.None,_=>Task.CompletedTask);
        Assert.Equal("Partial input",result.Error);
        Assert.Equal([AddonInput.PrepareControl],actions);
    }
}
