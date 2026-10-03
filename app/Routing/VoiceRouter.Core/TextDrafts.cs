using System.Text;
namespace VoiceRouter.Core;
public sealed record TextDraft(string Text,TextTarget? Target,bool Valid,string Explanation);
public static class TextDrafts
{
    public static TextDraft Prepare(Transcript transcript,TextTarget? target,bool fresh)
    {
        // Preserve the whole transcript: routing instructions are ordinary text in a search field.
        string text=transcript.Text;
        if(transcript.Status!=TranscriptionStatus.Success || string.IsNullOrWhiteSpace(text)) return new(text,target,false,"No speech text to insert.");
        if(!fresh || target is null) return new(text,target,false,"Fresh focused-field evidence is required.");
        if(target.Kind is not (TextFieldKind.AuctionHouse or TextFieldKind.Search) || string.IsNullOrWhiteSpace(target.Name)) return new(text,target,false,"Focused field is not verified for dictation.");
        if(target.Limit<=0 || target.Limit>4096) return new(text,target,false,"Verify this field's input limit first.");
        if(text.Contains('\r') || text.Contains('\n') || text.Contains('\0')) return new(text,target,false,"Edit line breaks or NULs before inserting.");
        int length=target.LimitIsBytes ? Encoding.UTF8.GetByteCount(text) : text.EnumerateRunes().Count();
        return length>target.Limit ? new(text,target,false,"Text exceeds the field limit. Edit it; nothing is truncated.") :
            new(text,target,true,"Focused verified search field; plain text, no chat command.");
    }
    public static bool SameTarget(TextTarget? before,TextTarget? after)=>before is not null && before==after;
}
