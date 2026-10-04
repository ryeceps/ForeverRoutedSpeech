using VoiceRouter.Core;

namespace SpeakForever.Dictation;

enum DraftInput { OpenChat, Paste }
sealed record OpenPasteResult(string? Error, bool Attempted);

static class OpenPasteWorkflow
{
    public static async Task<OpenPasteResult> RunAsync(
        Func<bool, bool, (string? Error, GameContext? Context)> inspect,
        Func<DraftInput, (string? Error, bool Attempted)> input,
        CancellationToken ct, Func<CancellationToken, Task>? delay = null)
    {
        delay ??= token => Task.Delay(50, token);
        bool attempted = false;
        try
        {
            ct.ThrowIfCancellationRequested();
            var initial = inspect(true, false);
            if (initial.Error is not null) return new(initial.Error, false);
            bool opening = initial.Context is { FocusedText: null, ChatInput: ChatInputState.Closed };
            if (opening)
            {
                var open = input(DraftInput.OpenChat);
                attempted |= open.Attempted;
                if (open.Error is not null) return new(open.Error, attempted);
            }
            for (int i = 0; i < 30; i++)
            {
                ct.ThrowIfCancellationRequested();
                var field = inspect(opening, opening);
                if (field.Error is not null) return new(field.Error, attempted);
                if (field.Context is { ChatInput: ChatInputState.Open } or { FocusedText: not null })
                {
                    ct.ThrowIfCancellationRequested();
                    var paste = input(DraftInput.Paste);
                    return new(paste.Error, attempted || paste.Attempted);
                }
                await delay(ct).ConfigureAwait(false);
            }
            return new("Chat did not open with confirmed focus. Nothing was pasted; open chat manually and recopy.", attempted);
        }
        catch (OperationCanceledException)
        {
            return new("Paste cancelled. Inspect the field before retrying.", attempted);
        }
    }
}
