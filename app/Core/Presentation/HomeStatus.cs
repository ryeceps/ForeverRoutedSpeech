namespace SpeakForever.Presentation;

/// <summary>How the Home tab's status light looks: good to go, working, waiting on you, or stuck.</summary>
public enum StatusTone
{
    Ready,
    Busy,
    Idle,
    Problem,
}

/// <summary>
/// The Home tab's big status: a headline, a line saying what to do next, and its colour. In
/// <see cref="Detail"/>, "{0}" is the open-chat buttons and "{1}" the dictate button, which the
/// app draws as the controller's own icons. It holds no UI types, so the rules are testable.
/// </summary>
public sealed record HomeStatus(string Headline, string Detail, StatusTone Tone)
{
    /// <summary>The one thing that matters most right now, from what the engine is doing.</summary>
    /// <param name="startError">Why the controller loop couldn't start, if it couldn't.</param>
    public static HomeStatus Of(string? startError, bool running, DictationPhase phase, bool hasModel, bool loadingModel,
                                bool controller, bool chatOpen, string? keyboardShortcut, bool autoSubmit = false)
    {
        string trigger = controller ? "{1}" : keyboardShortcut ?? "the shortcut";
        return startError is not null ? new("Can't start", startError, StatusTone.Problem)
            : !running ? new("Paused", "Turn on Active to dictate again.", StatusTone.Idle)
            : phase == DictationPhase.Listening ? new("Listening…", $"Pause when you're done, or press {trigger} to finish.", StatusTone.Busy)
            : phase == DictationPhase.Transcribing ? new("Transcribing", "Getting your words ready to paste.", StatusTone.Busy)
            : phase == DictationPhase.Ready ? new("Ready to paste", controller
                ? autoSubmit ? "Click {1} again to open chat, paste, and send the ready draft." : "Open native chat for automatic paste, then press A to send. In a focused field, click {1} to paste."
                : $"Press Ctrl+V in the chat box, check it, then send it. Or press {trigger} to cancel.", StatusTone.Ready)
            : loadingModel && !hasModel ? new("Loading your voice model", "This takes a few seconds, and longer the first time on a graphics card.", StatusTone.Busy)
            : !hasModel ? new("Get a voice model", "Speak Forever needs one to understand you. Download it on the Voice model tab.", StatusTone.Problem)
            : !controller && keyboardShortcut is { } key ? new("Ready", $"Press {key} and speak, then paste it into chat with Ctrl+V.", StatusTone.Ready)
            : !controller ? new("Connect a controller", "Or set a keyboard shortcut on the Controls tab.", StatusTone.Idle)
            : chatOpen ? new("Chat open", autoSubmit ? "Click {1} to record, again to finish, then again when ready to paste and send." : "Click {1} to record, again to finish, then again when ready to paste. Press A to send.", StatusTone.Ready)
            : new("Ready", autoSubmit ? "Keep moving: click {1} to record, again to finish, then again when ready to open chat, paste, and send." : "Keep moving: click {1} to record and again to finish. When ready, open native chat for automatic paste, then press A to send.", StatusTone.Ready);
    }
}
