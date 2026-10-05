using VoiceRouter.Core;

namespace SpeakForever.Core.Tests;

public sealed class ClassicChatDefaultsTests
{
    static GameContext Raw() => new(4, "test", 1, 1, GroupCategory.Solo, false,
        [new(1, "General", Destination.General), new(2, "Trade", Destination.Trade)],
        new Dictionary<Destination, string>(), 0, true, ChatInputState.Closed);

    [Fact]
    public void DefaultsProvideCommandsAnd200ByteCapWithoutMarkingRawContextVerified()
    {
        var raw = Raw();
        var effective = ClassicChatDefaults.Apply(raw);
        Assert.Empty(raw.VerifiedPrefixes);
        Assert.Equal(0, raw.MessageLimit);
        Assert.True(effective.UsesClassicDefaults);
        Assert.Equal(200, effective.MessageLimit);
        Assert.True(effective.LimitIsBytes);
        Assert.Equal("/say", effective.VerifiedPrefixes[Destination.Say]);
    }

    [Theory]
    [InlineData("In general, hello", "/1 hello")]
    [InlineData("In trade, selling cloth", "/2 selling cloth")]
    public void ExplicitNumberedChannelsWorkWithoutSetupCommands(string speech, string expected)
    {
        var context = ClassicChatDefaults.Apply(Raw());
        var route = new Router(new()).Decide(new(speech, TranscriptionStatus.Success), context, true);
        Assert.Equal(expected, Router.Draft(route.Message, route, context, true).ClipboardText);
    }

    [Fact]
    public void LiveNumbersTakePriorityAndUnjoinedChannelsStayUnavailable()
    {
        var context = ClassicChatDefaults.Apply(Raw() with { Channels = [new(7, "Trade", Destination.Trade)] });
        var router = new Router(new());
        var trade = router.Decide(new("In trade, hello", TranscriptionStatus.Success), context, true);
        Assert.Equal("/7 hello", Router.Draft(trade.Message, trade, context, true).ClipboardText);
        Assert.Equal(RouteReason.ConfirmationRequired, router.Decide(new("In general, hello", TranscriptionStatus.Success), context, true).Reason);
        Assert.Equal(RouteReason.ConfirmationRequired, router.Decide(new("Tell guild hello", TranscriptionStatus.Success), context, true).Reason);
    }

    [Theory]
    [InlineData("a", 200, true)]
    [InlineData("a", 201, false)]
    [InlineData("é", 100, true)]
    [InlineData("é", 101, false)]
    public void OversizedAsciiAndUnicodeRequireEditing(string token, int count, bool fits)
    {
        var context = ClassicChatDefaults.Apply(Raw());
        string text = string.Concat(Enumerable.Repeat(token, count));
        var route = new Router(new()).Decide(new(text, TranscriptionStatus.Success), context, true);
        Assert.Equal(fits, Router.Draft(text, route, context, true).Valid);
        Assert.Equal(text, route.Message);
    }

    [Fact]
    public void SmallerMeasuredLimitAndFocusedSearchArePreserved()
    {
        var target = new TextTarget("Auction search", TextFieldKind.AuctionHouse, 63, false);
        var context = ClassicChatDefaults.Apply(Raw() with { MessageLimit = 100, LimitIsBytes = false, FocusedText = target });
        Assert.Equal(100, context.MessageLimit);
        Assert.Same(target, context.FocusedText);
        Assert.Equal(63, context.FocusedText!.Limit);
    }

    [Fact]
    public void StaleContextStillRequiresConfirmation()
    {
        var context = ClassicChatDefaults.Apply(Raw());
        Assert.Equal(RouteReason.ConfirmationRequired, new Router(new()).Decide(new("In general, hello", TranscriptionStatus.Success), context, false).Reason);
        Assert.False(Router.StandaloneDraft(new(new string('a', 201), TranscriptionStatus.Success), ClassicChatDefaults.DraftByteLimit).Valid);
    }
}
