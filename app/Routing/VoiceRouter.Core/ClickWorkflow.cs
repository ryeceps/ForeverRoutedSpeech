namespace VoiceRouter.Core;
public enum ClickPhase { Idle, Recording, Preparing, Ready, NeedsEditing, Confirmed }
public enum ClickAction { None, StartRecording, FinishRecording, ConfirmDraft }

public sealed class ClickWorkflow
{
    public ClickPhase Phase { get; private set; }
    public long Generation { get; private set; }
    public ClickAction Click()
    {
        switch(Phase)
        {
            case ClickPhase.Idle:
            case ClickPhase.Confirmed: Generation++;Phase=ClickPhase.Recording;return ClickAction.StartRecording;
            case ClickPhase.Recording: Phase=ClickPhase.Preparing;return ClickAction.FinishRecording;
            case ClickPhase.Ready: Phase=ClickPhase.Confirmed;return ClickAction.ConfirmDraft;
            default: return ClickAction.None;
        }
    }
    public void Complete(long generation,bool success,bool valid)
    {
        if(generation!=Generation || Phase!=ClickPhase.Preparing) return;
        Phase=!success ? ClickPhase.Idle : valid ? ClickPhase.Ready : ClickPhase.NeedsEditing;
    }
    public void Edited(bool valid)
    { if(Phase is ClickPhase.Ready or ClickPhase.NeedsEditing) Phase=valid ? ClickPhase.Ready : ClickPhase.NeedsEditing; }
    public void RetryConfirmation() {if(Phase==ClickPhase.Confirmed) Phase=ClickPhase.Ready;}
    public void Cancel() {Generation++;Phase=ClickPhase.Idle;}
}

public sealed class ButtonEdge
{
    private bool initialized,down;
    private TimeSpan lastPress=TimeSpan.MinValue;
    public bool Observe(bool pressed,TimeSpan now)
    {
        // Connecting with the stick held must never begin a dictation.
        if(!initialized) {initialized=true;down=pressed;return false;}
        bool rising=pressed && !down;down=pressed;
        if(!rising || lastPress!=TimeSpan.MinValue && now-lastPress<TimeSpan.FromMilliseconds(120)) return false;
        lastPress=now;return true;
    }
    public void Disconnect() {initialized=false;down=false;lastPress=TimeSpan.MinValue;}
}
