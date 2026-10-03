namespace VoiceRouter.Core;

public static class PasteContext
{
    public static string? Validate(GameContext? original, GameContext? context, bool fresh)
    {
        if(!fresh || context is null || original is null)
            return "Fresh addon context is required before controller paste. You can still paste manually.";
        if(context.FocusedText?.Kind != TextFieldKind.AuctionHouse &&
            (context.FocusedText is not null || context.ChatInput != ChatInputState.Open))
            return "Focus the verified chat or Auction House field before pasting.";
        if(context.Session != original.Session || context.ClientBuild != original.ClientBuild ||
            context.Group != original.Group || context.InGuild != original.InGuild ||
            context.MessageLimit != original.MessageLimit || context.LimitIsBytes != original.LimitIsBytes ||
            context.FocusedText != original.FocusedText ||
            !context.Channels.SequenceEqual(original.Channels) ||
            context.VerifiedPrefixes.Count != original.VerifiedPrefixes.Count ||
            context.VerifiedPrefixes.Any(p => !original.VerifiedPrefixes.TryGetValue(p.Key,out var prefix) || prefix != p.Value))
            return "Game context changed. Review and recopy the draft before pasting.";
        return null;
    }
}
