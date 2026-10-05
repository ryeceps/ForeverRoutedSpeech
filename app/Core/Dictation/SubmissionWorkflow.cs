using SpeakForever.Routing;
using VoiceRouter.Core;

namespace SpeakForever.Dictation;
enum SubmissionInput { OpenChat, Paste, Submit, CloseChat }
sealed record SubmissionResult(string? Error,bool Attempted);

static class SubmissionWorkflow
{
    public static async Task<SubmissionResult> RunAsync(RoutedDraft draft,
        Func<bool,(string? Error,GameContext? Context)> inspect,
        Func<SubmissionInput,(string? Error,bool Attempted)> input,
        CancellationToken ct,Func<CancellationToken,Task>? delay=null)
    {
        delay ??= token=>Task.Delay(50,token);
        bool attempted=false;
        SubmissionResult Result(string? error)=>new(error,attempted);
        try
        {
            ct.ThrowIfCancellationRequested();
            var initial=inspect(true);
            if(initial.Error is not null) return Result(initial.Error);
            if(initial.Context is null || initial.Context.ProtocolVersion<4) return Result("Reload the updated addon before automatic submission.");
            if(initial.Context.FocusedText is null && initial.Context.ChatInput==ChatInputState.Closed)
            {
                var open=input(SubmissionInput.OpenChat);attempted|=open.Attempted;
                if(open.Error is not null) return Result(open.Error);
            }
            bool focused=false;
            for(int i=0;i<30;i++)
            {
                ct.ThrowIfCancellationRequested();
                var field=inspect(false);
                if(field.Error is null && field.Context is { } context)
                {
                    if(!SubmissionGate.Matches(context,"")) return Result("The target field is not empty or its contents are unknown. Clear it and recopy before retrying.");
                    focused=true;break;
                }
                await delay(ct).ConfigureAwait(false);
            }
            if(!focused) return Result("Chat did not open with confirmed focus. No paste or submit was requested.");
            ct.ThrowIfCancellationRequested();
            var paste=input(SubmissionInput.Paste);attempted|=paste.Attempted;
            if(paste.Error is not null) return Result(paste.Error);
            string? lastError=null;
            for(int i=0;i<30;i++)
            {
                ct.ThrowIfCancellationRequested();
                var echo=inspect(false);
                if(echo.Error is not null) return Result(echo.Error);
                lastError=echo.Context is null ? "Game context missing." : SubmissionGate.ReadyToSubmit(draft.ClipboardText!,draft.Message,echo.Context);
                if(lastError is null)
                {
                    ct.ThrowIfCancellationRequested();
                    var send=input(SubmissionInput.Submit);attempted|=send.Attempted;
                    if(send.Error is not null) return Result(send.Error);
                    bool closeRequested=false;
                    for(int closeCheck=0;closeCheck<30;closeCheck++)
                    {
                        await delay(ct).ConfigureAwait(false);
                        ct.ThrowIfCancellationRequested();
                        var after=inspect(true);
                        if(after.Error is not null) return Result("Submit requested, but closing stopped: "+after.Error);
                        if(after.Context is not { } current) return Result("Submit requested; chat closure could not be confirmed.");
                        if(current.ChatInput==ChatInputState.Closed && current.FocusedText is null) return Result(null);
                        // Wait for new addon evidence. Never discard unsent text or dismiss another field/menu.
                        if(!closeRequested && current.Heartbeat!=echo.Context!.Heartbeat &&
                            current.ChatInput==ChatInputState.Open && current.FocusedText is null && SubmissionGate.Matches(current,""))
                        {
                            ct.ThrowIfCancellationRequested();
                            var close=input(SubmissionInput.CloseChat);attempted|=close.Attempted;
                            if(close.Error is not null) return Result(close.Error);
                            closeRequested=true;
                        }
                    }
                    return Result("Submit requested, but chat closure was not confirmed. Close it with the game's Back button; no submission was repeated.");
                }
                await delay(ct).ConfigureAwait(false);
            }
            return Result(lastError+" Automatic submission stopped; inspect the field.");
        }
        catch(OperationCanceledException) {return Result("Submission cancelled. Inspect the field; no retry was made.");}
    }
}
