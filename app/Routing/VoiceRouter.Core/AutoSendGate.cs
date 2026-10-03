namespace VoiceRouter.Core;
public static class AutoSendGate
{
    public static string? BlockReason(bool enabled, bool newRecording, bool cancelled, GameContext? context, bool fresh, ChatDraft draft)
    {
        if (!enabled) return "Autosend off.";
        if (!newRecording) return "Edits and manual copies never autosend.";
        if (cancelled) return "Recording cancelled.";
        if (!draft.Valid || draft.ClipboardText is null) return "Draft is not ready to send.";
        if (context is null || !fresh) return "Fresh verified game context is required for autosend.";
        if (context.ChatInput != ChatInputState.Closed) return "Close the game chat box; its input state must be verified for autosend.";
        return null;
    }
}
