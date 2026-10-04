using SpeakForever.Dictation;
using VoiceRouter.Core;

namespace SpeakForever.Core.Tests;

public class OpenPasteWorkflowTests
{
    static GameContext Context(ChatInputState state) => new(3, "test", 1, 1, GroupCategory.Solo, false,
        [], new Dictionary<Destination, string> { [Destination.Say] = "/say" }, 255, true, state);

    [Fact]
    public async Task ClosedChatOpensWaitsPastesAndStops()
    {
        var actions = new List<DraftInput>();
        int reads = 0;
        var result = await OpenPasteWorkflow.RunAsync((_, _) =>
            (null, Context(++reads < 4 ? ChatInputState.Closed : ChatInputState.Open)),
            action => { actions.Add(action); return (null, true); }, CancellationToken.None, _ => Task.CompletedTask);
        Assert.Null(result.Error);
        Assert.Equal([DraftInput.OpenChat, DraftInput.Paste], actions);
    }

    [Fact]
    public async Task AlreadyOpenChatOnlyPastes()
    {
        var actions = new List<DraftInput>();
        var result = await OpenPasteWorkflow.RunAsync((_, _) => (null, Context(ChatInputState.Open)),
            action => { actions.Add(action); return (null, true); }, CancellationToken.None);
        Assert.Null(result.Error);
        Assert.Equal([DraftInput.Paste], actions);
    }

    [Fact]
    public async Task FocusedSearchOnlyPastesWithoutEnter()
    {
        var actions = new List<DraftInput>();
        var context = Context(ChatInputState.Closed) with { FocusedText = new("Search", TextFieldKind.Search, 255, true) };
        var result = await OpenPasteWorkflow.RunAsync((_, _) => (null, context),
            action => { actions.Add(action); return (null, true); }, CancellationToken.None);
        Assert.Null(result.Error);
        Assert.Equal([DraftInput.Paste], actions);
    }

    [Fact]
    public async Task StaleContextRequestsNoInput()
    {
        var result = await OpenPasteWorkflow.RunAsync((_, _) => ("stale", null),
            _ => throw new InvalidOperationException("Unexpected input"), CancellationToken.None);
        Assert.Equal("stale", result.Error);
        Assert.False(result.Attempted);
    }

    [Fact]
    public async Task FailureAfterOpeningDoesNotPaste()
    {
        int reads = 0;
        var actions = new List<DraftInput>();
        var result = await OpenPasteWorkflow.RunAsync((_, _) =>
            ++reads == 1 ? (null, Context(ChatInputState.Closed)) : ("context changed", null),
            action => { actions.Add(action); return (null, true); }, CancellationToken.None);
        Assert.Equal("context changed", result.Error);
        Assert.Equal([DraftInput.OpenChat], actions);
    }

    [Fact]
    public async Task OpenTimeoutDoesNotPasteOrRetryEnter()
    {
        var actions = new List<DraftInput>();
        var result = await OpenPasteWorkflow.RunAsync((_, _) => (null, Context(ChatInputState.Closed)),
            action => { actions.Add(action); return (null, true); }, CancellationToken.None, _ => Task.CompletedTask);
        Assert.NotNull(result.Error);
        Assert.Equal([DraftInput.OpenChat], actions);
    }

    [Fact]
    public async Task CancelAfterOpeningDoesNotPaste()
    {
        using var cancel = new CancellationTokenSource();
        var actions = new List<DraftInput>();
        var result = await OpenPasteWorkflow.RunAsync((_, _) => (null, Context(ChatInputState.Closed)),
            action => { actions.Add(action); cancel.Cancel(); return (null, true); }, cancel.Token);
        Assert.NotNull(result.Error);
        Assert.Equal([DraftInput.OpenChat], actions);
    }

    [Fact]
    public void OpeningAllowsNewActiveChatButKeepsSessionGuard()
    {
        var original = Context(ChatInputState.Closed);
        var opened = original with { ChatInput = ChatInputState.Open, ActiveDestination = Destination.Say };
        Assert.Null(PasteContext.ValidateOpenPaste(original, opened, true, true, true));
        Assert.NotNull(PasteContext.ValidateOpenPaste(original, opened with { Session = 2 }, true, true, true));
        Assert.NotNull(PasteContext.ValidateOpenPaste(original, opened, false, true, true));
    }

    [Fact]
    public void TemporarySayDraftRequiresSayAfterOpening()
    {
        var original = Context(ChatInputState.Closed) with { VerifiedPrefixes = new Dictionary<Destination, string>(), MessageLimit = 0,
            ActiveDestination = Destination.Say };
        Assert.Null(PasteContext.ValidateOpenPaste(original, original with { ActiveDestination = null }, true, true, false));
        Assert.Null(PasteContext.ValidateOpenPaste(original, original with { ChatInput = ChatInputState.Open }, true, true, true));
        Assert.NotNull(PasteContext.ValidateOpenPaste(original, original with { ChatInput = ChatInputState.Open, ActiveDestination = Destination.Guild }, true, true, true));
    }

    [Fact]
    public async Task RejectedChatOpenDoesNotPaste()
    {
        var actions = new List<DraftInput>();
        var result = await OpenPasteWorkflow.RunAsync((_, _) => (null, Context(ChatInputState.Closed)),
            action => { actions.Add(action); return ("input rejected", true); }, CancellationToken.None);
        Assert.Equal("input rejected", result.Error);
        Assert.Equal([DraftInput.OpenChat], actions);
    }

    [Fact]
    public async Task PartialPasteIsNeverRepeated()
    {
        int requests = 0;
        var result = await OpenPasteWorkflow.RunAsync((_, _) => (null, Context(ChatInputState.Open)),
            _ => { requests++; return ("partial paste", true); }, CancellationToken.None);
        Assert.Equal("partial paste", result.Error);
        Assert.True(result.Attempted);
        Assert.Equal(1, requests);
    }

    [Fact]
    public void UnknownFocusAndChangedGroupOrFieldRemainBlocked()
    {
        var original = Context(ChatInputState.Closed);
        Assert.NotNull(PasteContext.ValidateOpenPaste(original, original with { ChatInput = ChatInputState.Unknown }, true, true, false));
        Assert.NotNull(PasteContext.ValidateOpenPaste(original, original with { Group = GroupCategory.Party }, true, true, false));
        Assert.NotNull(PasteContext.ValidateOpenPaste(original, original with { ActivePanelUnsupported = true }, true, true, false));
        Assert.NotNull(PasteContext.ValidateOpenPaste(original, original with { FocusedText = new("Search", TextFieldKind.Search, 255, true) }, true, true, true));
    }
}
