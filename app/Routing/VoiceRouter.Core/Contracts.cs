namespace VoiceRouter.Core;

public enum Destination { Say, Guild, Party, Raid, Instance, General, Trade, LookingForGroup, Custom, Default }
public enum GroupCategory { Solo, Party, Raid, Instance }
public enum ChatInputState { Unknown, Closed, Open }
public enum TextFieldKind { AuctionHouse, Search, Unsupported }
public sealed record TextTarget(string Name, TextFieldKind Kind, int Limit, bool LimitIsBytes);
public enum TranscriptionStatus { Success, Silence, Cancelled, Failure }
public enum RouteReason { ExplicitInstruction, ModelInference, GroupDefault, ManualCorrection, ConfirmationRequired, ActivePanelDefault, SayDefault }
public sealed record Channel(int Id, string Name, Destination Kind);
public sealed record GameContext(int ProtocolVersion, string ClientBuild, uint Session, uint Heartbeat,
    GroupCategory Group, bool InGuild, IReadOnlyList<Channel> Channels,
    IReadOnlyDictionary<Destination, string> VerifiedPrefixes, int MessageLimit, bool LimitIsBytes, ChatInputState ChatInput = ChatInputState.Unknown,
    TextTarget? FocusedText = null, Destination? ActiveDestination = null, int? ActiveChannelId = null, bool ActivePanelUnsupported = false, int? InputBytes = null, uint? InputChecksum = null, bool UsesClassicDefaults = false,
    string? Zone = null, string? Subzone = null, bool? InCity = null, bool? Resting = null);
public sealed record Transcript(string Text, TranscriptionStatus Status);
public sealed record RouteDecision(Destination? Destination, int? ChannelId, IReadOnlyDictionary<Destination, double> Scores,
    RouteReason Reason, string Explanation, string Message, string? ChannelName = null);
public sealed record ChatDraft(string Message, string? Prefix, bool Valid, string Explanation)
{
    public string? ClipboardText => Valid && Prefix is not null ? Prefix + " " + Message : null;
}
public sealed record InferencePolicy(bool PublicValidated = false, double PublicThreshold = 1.01,
    double GuildThreshold = .9, double Margin = .2);
public static class Features
{
    // Shared verbatim with training/prepare.py. Context contains no volatile IDs or channel names.
    public static string Encode(string text, GameContext? context) =>
        string.Join(' ', text.ToLowerInvariant().Replace('\n', ' ').Replace('\r', ' ').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)) +
        $" ctx_group_{context?.Group.ToString().ToLowerInvariant() ?? "unknown"} ctx_guild_{(context?.InGuild == true ? "yes" : "no")}" +
        $" ctx_trade_{(context?.Channels.Any(c => c.Kind == Destination.Trade) == true ? "yes" : "no")}" +
        $" ctx_general_{(context?.Channels.Any(c => c.Kind == Destination.General) == true ? "yes" : "no")}" +
        $" ctx_lfg_{(context?.Channels.Any(c => c.Kind == Destination.LookingForGroup) == true ? "yes" : "no")}";
}
