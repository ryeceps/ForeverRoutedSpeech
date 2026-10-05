using VoiceRouter.Core;

namespace SpeakForever.Core.Tests;

public sealed class ExplicitChannelRoutingTests
{
    static GameContext Context() => new(3, "test", 1, 1, GroupCategory.Solo, true,
        [new(1, "General - Teldrassil", Destination.General), new(2, "Trade - City", Destination.Trade),
         new(4, "LookingForGroup", Destination.LookingForGroup), new(9, "Friends", Destination.Custom)],
        new Dictionary<Destination, string> { [Destination.Say] = "/say", [Destination.Custom] = "numbered" }, 255, true);

    [Theory]
    [InlineData("In general, hey guys, how's it going?", Destination.General, 1, "hey guys, how's it going?")]
    [InlineData("In General hey guys", Destination.General, 1, "hey guys")]
    [InlineData("In the general chat: hello", Destination.General, 1, "hello")]
    [InlineData("General, hello", Destination.General, 1, "hello")]
    [InlineData("In trade, WTS copper ore", Destination.Trade, 2, "WTS copper ore")]
    [InlineData("To trade chat sell copper ore", Destination.Trade, 2, "sell copper ore")]
    [InlineData("Say in trade that I need linen", Destination.Trade, 2, "I need linen")]
    [InlineData("In looking for group, need a tank", Destination.LookingForGroup, 4, "need a tank")]
    [InlineData("In LFG, need a tank", Destination.LookingForGroup, 4, "need a tank")]
    [InlineData("In Friends, meet at the bank", Destination.Custom, 9, "meet at the bank")]
    public void LeadingAudienceOverridesSayAndPreservesMessage(string speech, Destination destination, int channel, string message)
    {
        var context = Context();
        var route = new Router(new()).Decide(new(speech, TranscriptionStatus.Success), context, true);
        Assert.Equal(RouteReason.ExplicitInstruction, route.Reason);
        Assert.Equal(destination, route.Destination);
        Assert.Equal(channel, route.ChannelId);
        Assert.Equal(message, route.Message);
        Assert.Equal($"/{channel} {message}", Router.Draft(message, route, context, true).ClipboardText);
    }

    [Theory]
    [InlineData("I was talking in general chat yesterday.")]
    [InlineData("Don't post in trade, I am keeping the ore.")]
    [InlineData("General advice would help.")]
    [InlineData("Trade goods are expensive.")]
    public void MentionsAndNegationsDoNotBecomeInstructions(string speech)
    {
        var route = new Router(new()).Decide(new(speech, TranscriptionStatus.Success), Context(), true);
        Assert.Equal(Destination.Say, route.Destination);
        Assert.Equal(speech, route.Message);
    }

    [Fact]
    public void JoinedChannelNumberIsReadFromCurrentContext()
    {
        var context = Context() with { Channels = [new(6, "General - Zone", Destination.General)] };
        var route = new Router(new()).Decide(new("In general, hello", TranscriptionStatus.Success), context, true);
        Assert.Equal("/6 hello", Router.Draft(route.Message, route, context, true).ClipboardText);
    }

    [Fact]
    public void UnavailableOrStaleExplicitChannelNeverFallsBackToSay()
    {
        var router = new Router(new());
        var missing = router.Decide(new("In general, hello", TranscriptionStatus.Success), Context() with { Channels = [] }, true);
        Assert.Equal(RouteReason.ConfirmationRequired, missing.Reason);
        Assert.Equal(Destination.General, missing.Destination);
        var stale = router.Decide(new("In general, hello", TranscriptionStatus.Success), Context(), false);
        Assert.Equal(RouteReason.ConfirmationRequired, stale.Reason);
        Assert.False(Router.StandaloneDraft(new("In general, hello", TranscriptionStatus.Success)).Valid);
    }

    [Fact]
    public void SetupSkippedModeAllowsOnlyExplicitJoinedChannelsWithDraftCap()
    {
        var context = Context() with { VerifiedPrefixes = new Dictionary<Destination, string>(), MessageLimit = 0 };
        var (_, general) = Router.SetupSkippedDraft(new("In general, hello", TranscriptionStatus.Success), context);
        Assert.Equal("/1 hello", general.ClipboardText);
        var (_, trade) = Router.SetupSkippedDraft(new("In trade, selling linen", TranscriptionStatus.Success), context);
        Assert.Equal("/2 selling linen", trade.ClipboardText);
        var (_, inferred) = Router.SetupSkippedDraft(new("selling linen", TranscriptionStatus.Success), context);
        Assert.Equal("/say selling linen", inferred.ClipboardText);
        var (_, missing) = Router.SetupSkippedDraft(new("In general, hello", TranscriptionStatus.Success), context with { Channels = [] });
        Assert.False(missing.Valid);
        var (_, empty) = Router.SetupSkippedDraft(new("In general", TranscriptionStatus.Success), context);
        Assert.False(empty.Valid);
        var (_, oversized) = Router.SetupSkippedDraft(new("In general, " + new string('x', 4097), TranscriptionStatus.Success), context);
        Assert.False(oversized.Valid);
    }
}
