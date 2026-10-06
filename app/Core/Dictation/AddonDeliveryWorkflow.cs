namespace SpeakForever.Dictation;

enum AddonInput { PrepareInbox, Paste, CancelInbox }
sealed record AddonDeliveryResult(string? Error, bool Attempted);

/// <summary>One deliberate click delivers one draft. All game decisions stay in the addon.</summary>
static class AddonDeliveryWorkflow
{
    public static async Task<AddonDeliveryResult> RunAsync(Func<string?> inspect,
        Func<AddonInput,(string? Error,bool Attempted)> input, CancellationToken ct,
        Func<CancellationToken,Task>? delay=null)
    {
        delay ??= token=>Task.Delay(120,token);
        bool attempted=false;
        try
        {
            ct.ThrowIfCancellationRequested();
            if (inspect() is { } before) return new(before,false);
            var prepare=input(AddonInput.PrepareInbox); attempted|=prepare.Attempted;
            if (prepare.Error is not null) return new(prepare.Error,attempted);
            await delay(ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (inspect() is { } after) return new(after,attempted);
            var paste=input(AddonInput.Paste); attempted|=paste.Attempted;
            return new(paste.Error,attempted);
        }
        catch (OperationCanceledException) { return new("Draft delivery cancelled.",attempted); }
        finally
        {
            // Best effort only; the inbox also expires locally if focus/clipboard checks stop delivery.
            if (attempted && ct.IsCancellationRequested) input(AddonInput.CancelInbox);
        }
    }
}
