namespace VoiceRouter.Core;

public static class PasteContext
{
    public static string? ValidateOpenPaste(GameContext? original, GameContext? context, bool fresh, bool allowClosed, bool opening)
    {
        if (context is null || original is null) return Validate(original, context, fresh);
        if (context.FocusedText is null && context.ChatInput == ChatInputState.Unknown)
            return "Chat focus is unknown. Open chat manually before pasting.";
        var expected = original;
        var check = context;
        if (opening && context.FocusedText is null && !context.ActivePanelUnsupported &&
            original.VerifiedPrefixes.Count > 0 && original.MessageLimit > 0)
            expected = original with { ActiveDestination = context.ActiveDestination, ActiveChannelId = context.ActiveChannelId };
        if (allowClosed && context.FocusedText is null && context.ChatInput == ChatInputState.Closed)
            check = context with { ChatInput = ChatInputState.Open, ActiveDestination = expected.ActiveDestination,
                ActiveChannelId = expected.ActiveChannelId };
        return Validate(expected, check, fresh);
    }

    public static string? Validate(GameContext? original, GameContext? context, bool fresh)
    {
        if(!fresh || context is null || original is null)
            return "Fresh addon context is required before controller paste. You can still paste manually.";
        if(context.FocusedText?.Kind is not (TextFieldKind.AuctionHouse or TextFieldKind.Search) &&
            (context.FocusedText is not null || context.ChatInput != ChatInputState.Open))
            return "Focus the verified chat or Auction House field before pasting.";
        if(context.Session != original.Session || context.ClientBuild != original.ClientBuild ||
            context.Group != original.Group || context.InGuild != original.InGuild ||
            context.MessageLimit != original.MessageLimit || context.LimitIsBytes != original.LimitIsBytes || context.UsesClassicDefaults != original.UsesClassicDefaults ||
            context.FocusedText != original.FocusedText ||
            context.ActiveDestination != original.ActiveDestination || context.ActiveChannelId != original.ActiveChannelId ||
            context.ActivePanelUnsupported != original.ActivePanelUnsupported ||
            !context.Channels.SequenceEqual(original.Channels) ||
            context.VerifiedPrefixes.Count != original.VerifiedPrefixes.Count ||
            context.VerifiedPrefixes.Any(p => !original.VerifiedPrefixes.TryGetValue(p.Key,out var prefix) || prefix != p.Value))
            return "Game context changed. Review and recopy the draft before pasting.";
        return null;
    }
}
