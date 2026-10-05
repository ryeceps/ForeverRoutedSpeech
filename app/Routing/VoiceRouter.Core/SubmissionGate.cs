using System.Text;
namespace VoiceRouter.Core;

public static class SubmissionGate
{
    public static bool Matches(GameContext context,string text)
    {
        var bytes=Encoding.UTF8.GetBytes(text);
        return context.InputBytes==bytes.Length && context.InputChecksum==StatusProtocol.Checksum(bytes);
    }
    public static string? ReadyToSubmit(string clipboard,string message,GameContext context)
    {
        if(context.FocusedText is TextTarget target)
        {
            if(target.Kind!=TextFieldKind.AuctionHouse) return "Automatic submit supports verified Auction House search only.";
            return Matches(context,clipboard) ? null : "Waiting for the pasted search text to be confirmed.";
        }
        if(context.ChatInput!=ChatInputState.Open || context.ActivePanelUnsupported) return "Chat is not focused.";
        int space=clipboard.IndexOf(' ');
        if(space<0) return "Invalid chat draft.";
        string prefix=clipboard[..space].ToLowerInvariant();
        Destination? audience=prefix switch {"/say" or "/s"=>Destination.Say,"/g" or "/guild"=>Destination.Guild,
            "/p" or "/party"=>Destination.Party,"/raid" or "/ra"=>Destination.Raid,"/i" or "/instance"=>Destination.Instance,_=>null};
        if(audience is Destination expected && context.ActiveDestination!=expected) return "Waiting for the intended chat audience.";
        if(audience is null && (!int.TryParse(prefix.TrimStart('/'),out int id) || context.ActiveChannelId!=id)) return "Waiting for the intended numbered channel.";
        return Matches(context,message) || Matches(context,clipboard) ? null : "Waiting for the pasted chat text to be confirmed.";
    }
}
