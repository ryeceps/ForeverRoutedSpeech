using System.Text;
using System.Text.RegularExpressions;

namespace VoiceRouter.Core;

public sealed class Router(InferencePolicy policy)
{
    private static readonly IReadOnlyDictionary<Destination, double> EmptyScores = new Dictionary<Destination, double>();
    private static readonly (Destination Destination, string Name)[] Names =
    [ (Destination.Say, "everyone around me"), (Destination.Say, "everyone nearby"), (Destination.LookingForGroup, "looking for group"),
      (Destination.Instance, "instance"), (Destination.General, "general"), (Destination.Guild, "guild"),
      (Destination.Party, "party"), (Destination.Raid, "raid"), (Destination.Trade, "trade"), (Destination.Say, "say") ];

    public RouteDecision Decide(Transcript transcript, GameContext? context, bool fresh,
        IReadOnlyDictionary<Destination, double>? scores = null)
    {
        scores ??= EmptyScores;
        string message = transcript.Text.Trim();
        if (transcript.Status != TranscriptionStatus.Success || message.Length == 0)
            return new(null, null, scores, RouteReason.ConfirmationRequired, "No successful speech transcript; clipboard retained.", message);
        var explicitRoute = ParseExplicit(message, context, scores);
        if (explicitRoute is not null)
        {
            if (explicitRoute.Destination is null) return explicitRoute;
            if (!fresh || context is null) return explicitRoute with { Reason = RouteReason.ConfirmationRequired,
                Explanation = "Context missing or stale. Confirm the requested destination before copying." };
            if (!Available(context, explicitRoute.Destination!.Value, explicitRoute.ChannelId))
                return explicitRoute with { Reason = RouteReason.ConfirmationRequired, Explanation = "Requested destination unavailable. Choose another destination." };
            return explicitRoute;
        }
        if (context is null || !fresh)
            return new(null, null, scores, RouteReason.ConfirmationRequired, "Context missing or stale. Confirm a destination before copying.", message);
        var ranked = scores.Where(s => double.IsFinite(s.Value) && s.Value >= 0 && s.Value <= 1).OrderByDescending(s => s.Value).ToArray();
        if (ranked.Length > 0)
        {
            var best = ranked[0];
            bool publicRoute = best.Key is Destination.Trade or Destination.General or Destination.LookingForGroup;
            bool trained = publicRoute || best.Key == Destination.Guild;
            double threshold = publicRoute ? policy.PublicThreshold : policy.GuildThreshold;
            double runner = ranked.Length > 1 ? ranked[1].Value : 0;
            var channel = context.Channels.FirstOrDefault(c => c.Kind == best.Key);
            if (trained && (!publicRoute || policy.PublicValidated) && best.Value >= threshold && best.Value - runner >= policy.Margin &&
                Available(context, best.Key, channel?.Id))
                return new(best.Key, channel?.Id, scores, RouteReason.ModelInference, "Clear trained audience intent (model score).", message, channel?.Name);
        }
        var fallback = context.Group switch
        {
            GroupCategory.Instance => new[] { Destination.Instance, Destination.Raid, Destination.Party, Destination.Say },
            GroupCategory.Raid => new[] { Destination.Raid, Destination.Party, Destination.Say },
            GroupCategory.Party => new[] { Destination.Party, Destination.Say },
            _ => new[] { Destination.Say }
        };
        foreach (var destination in fallback)
            if (Available(context, destination, null)) return new(destination, null, scores, RouteReason.GroupDefault, "Available group default.", message);
        return new(null, null, scores, RouteReason.ConfirmationRequired, "No verified destination is available.", message);
    }

    private static RouteDecision? ParseExplicit(string text, GameContext? context, IReadOnlyDictionary<Destination, double> scores)
    {
        var candidates = Names.Concat(context?.Channels.Where(c => c.Kind == Destination.Custom).Select(c => (Destination.Custom, c.Name)) ?? []);
        foreach (var (destination, name) in candidates.OrderByDescending(n => n.Item2.Length))
        {
            var match = Regex.Match(text, @"^(?:tell|say to|ask in|speak to|send to)\s+(?:the\s+)?" + Regex.Escape(name) + @"(?:\s+chat)?(?:[,.:]\s*|\s+|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success) continue;
            string message = text[match.Length..].Trim();
            if (message.StartsWith("that ", StringComparison.OrdinalIgnoreCase)) message = message[5..];
            var channel = context?.Channels.FirstOrDefault(c => c.Kind == destination && (destination != Destination.Custom || c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
            return new(destination, channel?.Id, scores, RouteReason.ExplicitInstruction, "Explicit destination instruction.", message, channel?.Name ?? (destination == Destination.Custom ? name : null));
        }
        // Unknown named audience is an explicit request, never quietly sent to the group.
        var unknown = Regex.Match(text, @"^(?:ask in|send to|speak to)\s+(?:the\s+)?(?<name>[^,:.]+?)(?:\s+chat)?[,.:]\s*(?<message>.*)$", RegexOptions.IgnoreCase);
        if (unknown.Success) return new(Destination.Custom, null, scores, RouteReason.ConfirmationRequired,
            "Requested named channel is not joined. Choose another destination.", unknown.Groups["message"].Value, unknown.Groups["name"].Value);
        return Regex.IsMatch(text, @"^(?:ask in|send to|speak to)\s+", RegexOptions.IgnoreCase) ?
            new(null, null, scores, RouteReason.ConfirmationRequired, "Unrecognized explicit destination. Select an audience; retained the complete transcript.", text) : null;
    }

    public static bool Available(GameContext context, Destination destination, int? channelId)
    {
        if (destination is Destination.General or Destination.Trade or Destination.LookingForGroup or Destination.Custom)
            return context.VerifiedPrefixes.ContainsKey(Destination.Custom) && context.Channels.Any(c => c.Id == channelId && c.Kind == destination);
        if (!context.VerifiedPrefixes.ContainsKey(destination)) return false;
        return destination switch
        {
            Destination.Guild => context.InGuild,
            Destination.Party => context.Group is GroupCategory.Party or GroupCategory.Raid or GroupCategory.Instance,
            Destination.Raid => context.Group is GroupCategory.Raid or GroupCategory.Instance,
            Destination.Instance => context.Group == GroupCategory.Instance,
            _ => destination == Destination.Say
        };
    }

    public static ChatDraft Draft(string message, RouteDecision decision, GameContext? context, bool fresh,
        bool manuallyConfirmed = false, string? manualPrefix = null, int manualLimit = 0, bool manualBytes = true)
    {
        if (decision.Destination is null) return new(message, null, false, decision.Explanation);
        if (!fresh && !manuallyConfirmed) return new(message, null, false, "Confirm destination with missing or stale context.");
        if (decision.Reason == RouteReason.ConfirmationRequired && !manuallyConfirmed) return new(message, null, false, decision.Explanation);
        string? prefix = null;
        if (fresh && context is not null)
        {
            if (!Available(context, decision.Destination.Value, decision.ChannelId)) return new(message, null, false, "Destination unavailable.");
            prefix = decision.ChannelId is int id ? "/" + id : context.VerifiedPrefixes[decision.Destination.Value];
        }
        else prefix = manualPrefix;
        if (prefix is null || !Regex.IsMatch(prefix, @"^/(?:say|s|guild|g|party|p|raid|ra|instance|i|[1-9][0-9]*)$", RegexOptions.IgnoreCase))
            return new(message, null, false, "A verified chat prefix is required.");
        if (!fresh)
        {
            var aliases = decision.Destination switch
            {
                Destination.Say => new[]{"/say","/s"}, Destination.Guild => new[]{"/g","/guild"},
                Destination.Party => new[]{"/p","/party"}, Destination.Raid => new[]{"/raid","/ra"},
                Destination.Instance => new[]{"/i","/instance"}, _ => decision.ChannelId is int id ? new[]{"/"+id} : []
            };
            if (!aliases.Contains(prefix.ToLowerInvariant())) return new(message,null,false,"Confirmed prefix does not match the selected destination.");
        }
        if (string.IsNullOrWhiteSpace(message)) return new(message, prefix, false, "Empty message; clipboard retained.");
        // Prevent pasted line breaks and slash-command interpretation without rewriting the user's speech.
        if (message.Contains('\n') || message.Contains('\r') || message.Contains('\0') || message.TrimStart().StartsWith('/'))
            return new(message, prefix, false, "Edit line breaks, NULs, or a leading slash before copying.");
        int limit = fresh && context is not null ? context.MessageLimit : manualLimit;
        bool bytes = fresh && context is not null ? context.LimitIsBytes : manualBytes;
        int length = bytes ? Encoding.UTF8.GetByteCount(message) : message.EnumerateRunes().Count();
        if (limit <= 0) return new(message, prefix, false, "Client message limit is unverified.");
        return length > limit ? new(message, prefix, false, $"Message exceeds the verified {limit} {(bytes ? "byte" : "character")} limit. Edit before copying.") :
            new(message, prefix, true, decision.Explanation);
    }
}
