using VoiceRouter.Core;

namespace SpeakForever.Core.Tests;

public class AutomaticAudienceTests
{
    static GameContext Context(GroupCategory group = GroupCategory.Solo) => ClassicChatDefaults.Apply(new(
        4, "test", 1, 1, group, false, [new(6, "General - Zone", Destination.General)],
        new Dictionary<Destination, string>(), 0, true, ChatInputState.Closed));
    static RouteDecision Route(GameContext context, string text = "Hello everyone") =>
        new Router(new()).Decide(new(text, TranscriptionStatus.Success), context, true);

    [Theory]
    [InlineData(GroupCategory.Party, Destination.Party, "/p")]
    [InlineData(GroupCategory.Raid, Destination.Raid, "/raid")]
    [InlineData(GroupCategory.Instance, Destination.Instance, "/i")]
    public void ClosedChatAutomaticallyUsesCurrentGroup(GroupCategory group, Destination destination, string prefix)
    {
        var context = Context(group) with { ActiveDestination = Destination.Say };
        var route = Route(context);
        Assert.Equal(destination, route.Destination);
        Assert.Equal(RouteReason.GroupDefault, route.Reason);
        Assert.Equal(prefix + " Hello everyone", Router.Draft(route.Message, route, context, true).ClipboardText);
    }

    [Fact]
    public void SoloKeepsSelectedGeneralWithoutOpeningChatOrSetup()
    {
        var context = Context() with { ActiveDestination = Destination.General, ActiveChannelId = 6 };
        var route = Route(context);
        Assert.Equal(RouteReason.ActivePanelDefault, route.Reason);
        Assert.Equal("/6 Hello everyone", Router.Draft(route.Message, route, context, true).ClipboardText);
    }

    [Fact]
    public void OpenGeneralAndExplicitGeneralOverridePartyButRememberedGeneralDoesNot()
    {
        var context = Context(GroupCategory.Party) with { ActiveDestination = Destination.General, ActiveChannelId = 6 };
        Assert.Equal(Destination.Party, Route(context).Destination);
        Assert.Equal(Destination.General, Route(context with { ChatInput = ChatInputState.Open }).Destination);
        var route = Route(context, "In General, hello");
        Assert.Equal("/6 hello", Router.Draft(route.Message, route, context, true).ClipboardText);
    }

    [Fact]
    public void LeavingGroupOrChannelReevaluatesDefault()
    {
        var context = Context(GroupCategory.Party);
        Assert.Equal(Destination.Party, Route(context).Destination);
        Assert.Equal(Destination.Say, Route(context with { Group = GroupCategory.Solo }).Destination);
        context = Context() with { ActiveDestination = Destination.General, ActiveChannelId = 6, Channels = [] };
        Assert.Equal(Destination.Say, Route(context).Destination);
        Assert.Equal(RouteReason.ConfirmationRequired, Route(context, "In General, hello").Reason);
    }

    [Fact]
    public void JoinedGeneralAloneDoesNotBecomeAnUnrequestedPublicAudience()
    {
        Assert.Equal(Destination.Say, Route(Context()).Destination);
        Assert.Equal(RouteReason.ConfirmationRequired, new Router(new()).Decide(
            new("Hello", TranscriptionStatus.Success), Context(GroupCategory.Party), false).Reason);
    }
}
