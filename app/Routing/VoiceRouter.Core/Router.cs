using System.Text;
using System.Text.RegularExpressions;

namespace VoiceRouter.Core;

public sealed class Router(InferencePolicy policy)
{
    private static readonly IReadOnlyDictionary<Destination, double> EmptyScores = new Dictionary<Destination, double>();
    private static readonly (Destination Destination, string Name)[] Names =
    [ (Destination.Say, "everyone around me"), (Destination.Say, "everyone nearby"), (Destination.LookingForGroup, "looking for group"),
      (Destination.LookingForGroup, "lfg"), (Destination.Instance, "instance"), (Destination.General, "general"), (Destination.Guild, "guild"),
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
        if(context.ActivePanelUnsupported)
            return new(null,null,scores,RouteReason.ConfirmationRequired,"Active chat audience is unsupported. Select a destination.",message);
        if(context.ChatInput == ChatInputState.Open && context.ActiveDestination is Destination active)
        {
            var channel=context.Channels.FirstOrDefault(c=>c.Id==context.ActiveChannelId && c.Kind==active);
            if(!Available(context,active,context.ActiveChannelId))
                return new(active,context.ActiveChannelId,scores,RouteReason.ConfirmationRequired,"Active chat destination unavailable. Select a destination.",message,channel?.Name);
            return new(active,context.ActiveChannelId,scores,RouteReason.ActivePanelDefault,"Current active chat panel.",message,channel?.Name);
        }
        if(Available(context,Destination.Say,null))
            return new(Destination.Say,null,scores,RouteReason.SayDefault,"Say default; no active chat destination known.",message);
        return new(null, null, scores, RouteReason.ConfirmationRequired, "No verified destination is available.", message);
    }

    /// <summary>Preview/copy without game context. The 4096-byte cap is a draft cap, not a verified game limit.</summary>
    public static ChatDraft StandaloneDraft(Transcript transcript)
    {
        string message=transcript.Text.Trim();
        if(transcript.Status!=TranscriptionStatus.Success || message.Length==0)
            return new(message,null,false,"No successful speech transcript; clipboard retained.");
        var instruction=ParseExplicit(message,null,EmptyScores);
        if(instruction is not null && instruction.Destination!=Destination.Say)
            return new(instruction.Message,null,false,"Game context is unavailable. Confirm the explicitly requested destination; it was not changed to Say.");
        if(instruction is not null) message=instruction.Message;
        var decision=new RouteDecision(Destination.Say,null,EmptyScores,RouteReason.SayDefault,
            "Standalone Say draft; no game context. The game prefix and length limit are unverified. Controller paste is disabled until game context is verified.",message);
        var draft=Draft(message,decision,null,false,true,"/say",4096,true);
        return draft.Valid ? draft with {Explanation=decision.Explanation} : draft;
    }

    /// <summary>Opt-in setup-skipped drafts: Say or an explicit joined numbered channel, never inferred public routing.</summary>
    public static (RouteDecision Decision, ChatDraft Draft) SetupSkippedDraft(Transcript transcript, GameContext context)
    {
        string text = transcript.Text.Trim();
        var instruction = ParseExplicit(text, context, EmptyScores);
        if (instruction is null || instruction.Destination == Destination.Say)
        {
            var say = StandaloneDraft(transcript);
            return (new(Destination.Say, null, EmptyScores, RouteReason.SayDefault, say.Explanation, say.Message), say);
        }
        if (instruction.Destination is not (Destination.General or Destination.Trade or Destination.LookingForGroup or Destination.Custom) ||
            instruction.ChannelId is null || transcript.Status != TranscriptionStatus.Success)
            return (instruction, new(instruction.Message, null, false,
                "Requested audience is unavailable or requires chat setup. Choose another destination; it was not changed to Say."));
        // The user explicitly opted out of compatibility setup. This is a draft cap, not a measured game limit.
        var temporary = context with { VerifiedPrefixes = new Dictionary<Destination, string> { [Destination.Custom] = "numbered" },
            MessageLimit = context.MessageLimit > 0 ? context.MessageLimit : 4096,
            LimitIsBytes = context.MessageLimit > 0 ? context.LimitIsBytes : true };
        instruction = instruction with { Explanation = "Explicit joined channel; compatibility setup skipped. Prefix and game length limit are unverified." };
        return (instruction, Draft(instruction.Message, instruction, temporary, true));
    }

    private static RouteDecision? ParseExplicit(string text, GameContext? context, IReadOnlyDictionary<Destination, double> scores)
    {
        var candidates = Names.Concat(context?.Channels.Where(c => c.Kind == Destination.Custom).Select(c => (Destination.Custom, c.Name)) ?? []);
        foreach (var (destination, name) in candidates.OrderByDescending(n => n.Item2.Length))
        {
            var match = Regex.Match(text, @"^(?:(?:tell|say to|say in|ask in|speak to|send to|send in|post in|in|to)\s+(?:the\s+)?|(?=" + Regex.Escape(name) + @"(?:\s+chat)?[,.:]))" + Regex.Escape(name) + @"(?:\s+chat)?(?:[,.:]\s*|\s+|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
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
