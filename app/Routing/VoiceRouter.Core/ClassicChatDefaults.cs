namespace VoiceRouter.Core;

/// <summary>Assumed Classic command prefixes and a conservative app draft cap, not a measured client limit.</summary>
public static class ClassicChatDefaults
{
    public const int DraftByteLimit = 200;

    public static GameContext Apply(GameContext context)
    {
        var prefixes = new Dictionary<Destination, string>
        {
            [Destination.Say] = "/say", [Destination.Guild] = "/g", [Destination.Party] = "/p",
            [Destination.Raid] = "/raid", [Destination.Instance] = "/i", [Destination.Custom] = "numbered"
        };
        foreach (var pair in context.VerifiedPrefixes) prefixes[pair.Key] = pair.Value;
        // A smaller measured character limit is conservatively used as a byte cap too.
        return context with { VerifiedPrefixes = prefixes, MessageLimit = context.MessageLimit > 0
            ? Math.Min(DraftByteLimit, context.MessageLimit) : DraftByteLimit, LimitIsBytes = true, UsesClassicDefaults = true };
    }
}
