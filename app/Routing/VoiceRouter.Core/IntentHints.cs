namespace VoiceRouter.Core;

/// <summary>Text-only model hints. Client availability and reply context are resolved by Lua.</summary>
public static class IntentHints
{
    public static string Select(string text,IEnumerable<(Destination Kind,double Score)> scores,InferencePolicy policy)
    {
        var ranked=scores.Where(s=>double.IsFinite(s.Score) && s.Score is >=0 and <=1).OrderByDescending(s=>s.Score).ToArray();
        if(ranked.Length==0) return "default";
        var best=ranked[0];
        bool publicRoute=best.Kind is Destination.General or Destination.Trade or Destination.LookingForGroup;
        bool allowed=best.Kind==Destination.Guild && Features.HasGuildAddress(text) || publicRoute;
        // An older report's disabled sentinel is not an attainable confidence threshold.
        double publicThreshold=policy.PublicThreshold>1 ? .8 : Math.Max(.8,policy.PublicThreshold);
        double threshold=publicRoute ? publicThreshold : policy.GuildThreshold;
        if(allowed && best.Score>=threshold && best.Score-(ranked.Length>1 ? ranked[1].Score : 0)>=Math.Max(.1,policy.Margin))
            return "i:"+best.Kind.ToString().ToLowerInvariant();
        return "default";
    }
}
