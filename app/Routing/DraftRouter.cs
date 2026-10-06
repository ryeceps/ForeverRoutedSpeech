using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VoiceRouter.Core;
using VoiceRouter.App;

namespace SpeakForever.Routing;

public sealed record RoutedDraft(string Message,string? ClipboardText,string Destination,string Reason,double RoutingMilliseconds)
{
    public string AddonHint { get; init; } = "default";
    public bool Ready => ClipboardText is not null;
}

/// <summary>Persistent fastText only. The host retains sole ownership of Whisper.</summary>
public sealed class DraftRouter : IDisposable
{
    [DllImport("voice_router_native",CallingConvention=CallingConvention.Cdecl)]
    private static extern nint wvr_router_create([MarshalAs(UnmanagedType.LPUTF8Str)] string model);
    [DllImport("voice_router_native",CallingConvention=CallingConvention.Cdecl)]
    private static extern int wvr_predict(nint handle,[MarshalAs(UnmanagedType.LPUTF8Str)] string text,byte[] output,int capacity);
    [DllImport("voice_router_native",CallingConvention=CallingConvention.Cdecl)] private static extern void wvr_destroy(nint handle);
    private readonly object gate=new();
    private readonly ContextTracker tracker=new();
    private readonly Stopwatch clock=Stopwatch.StartNew();
    private readonly System.Threading.Timer? timer;
    public bool UsesAddonRouting { get; }
    private bool textOnlyFeatures;
    private InferencePolicy intentPolicy = new();
    private nint handle;
    private Router router=new(new());
    private string modelError="Classifier not loaded.";
    private bool disposed;
    private GameContext? copiedContext;
    private string? detachedTranscript;
    private string captureError = "Waiting for addon context.";
    public bool CanRecoverDetachedDraft { get { lock(gate) return detachedTranscript is not null && tracker.IsFresh(clock.Elapsed); } }
    public RoutedDraft RecoverDetachedDraft()
    {
        lock(gate)
        {
            if(detachedTranscript is null || !tracker.IsFresh(clock.Elapsed))
                return new("", null, "Unconfirmed", "Waiting for addon context. The draft is retained.", 0);
            var source=detachedTranscript;
            var recovered=Prepare(source);
            detachedTranscript=source; // Retry remains possible until the clipboard update succeeds.
            return recovered;
        }
    }
    public void CompleteDetachedRecovery() { lock(gate) detachedTranscript=null; }
    private readonly Func<GameContext> readContext;
    private readonly Func<bool> allowUnverifiedSayDrafts;
    private readonly Func<bool> useClassicDefaults;
    public DraftRouter(string folder, Func<GameContext>? contextReader = null, Func<bool>? allowUnverifiedSayDrafts = null, Func<bool>? useClassicDefaults = null)
    {
        UsesAddonRouting = contextReader is null;
        readContext = contextReader ?? (() => WindowsCapture.ReadAuto(Settings.Load()));
        this.allowUnverifiedSayDrafts = allowUnverifiedSayDrafts ?? (() => Settings.Load().AllowUnverifiedSayDrafts);
        this.useClassicDefaults = useClassicDefaults ?? (() => contextReader is null && Settings.Load().UseClassicChatDefaults);
        try
        {
            using var policy=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"router-policy.json")));
            var p=policy.RootElement;string model=Path.Combine(folder,"router.bin");
            using var stream=File.OpenRead(model);
            if(!Convert.ToHexString(SHA256.HashData(stream)).Equals(p.GetProperty("model_sha256").GetString(),StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Classifier checksum mismatch.");
            handle=wvr_router_create(model);
            if(handle==0) throw new InvalidOperationException("Classifier could not load.");
            router=new(new(p.GetProperty("public_validated").GetBoolean(),p.GetProperty("public_threshold").GetDouble(),p.GetProperty("guild_threshold").GetDouble(),p.GetProperty("margin").GetDouble()));
            intentPolicy = new(p.GetProperty("public_validated").GetBoolean(),p.GetProperty("public_threshold").GetDouble(),p.GetProperty("guild_threshold").GetDouble(),p.GetProperty("margin").GetDouble());
            textOnlyFeatures = p.TryGetProperty("feature_mode",out var mode) && mode.GetString()=="text_only";
            modelError="";
        }
        catch(Exception e) when(e is IOException or JsonException or InvalidOperationException or DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {modelError=e.Message;}
        Poll();
        if(!UsesAddonRouting) timer=new(_=>Poll(),null,TimeSpan.FromMilliseconds(250),TimeSpan.FromMilliseconds(250));
    }
    public void RefreshContext() => Poll();
    public bool ClassifierLoaded => handle != 0;
    public bool ChatDraftRecordingAvailable
    {
        get {lock(gate) return UsesAddonRouting || tracker.Current?.FocusedText is null;}
    }
    public bool FocusedFieldAvailable
    {
        get {lock(gate) return tracker.IsFresh(clock.Elapsed) && tracker.Current?.FocusedText?.Kind is TextFieldKind.AuctionHouse or TextFieldKind.Search;}
    }
    private void Poll()
    {
        lock(gate)
        {
            if(disposed || UsesAddonRouting) return;
            try
            {
                var context = readContext();
                tracker.Accept(useClassicDefaults() ? ClassicChatDefaults.Apply(context) : context, clock.Elapsed);
                captureError = "";
            }
            catch(Exception e) when(e is IOException or FormatException or ExternalException or ArgumentException or InvalidOperationException)
            {captureError=e.Message;tracker.Invalidate();}
        }
    }
    public RoutedDraft Prepare(string text)
    {
        lock(gate)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            if(UsesAddonRouting) return PrepareForAddon(text);
            copiedContext=tracker.Current;
            detachedTranscript=null;
            var timing=Stopwatch.StartNew();
            var context=tracker.Current;bool fresh=tracker.IsFresh(clock.Elapsed);
            if(context?.FocusedText is TextTarget target)
            {
                var field=TextDrafts.Prepare(new(text,TranscriptionStatus.Success),target,fresh);
                return new(text,field.Valid ? text : null,target.Name,field.Explanation,timing.Elapsed.TotalMilliseconds);
            }
            if(!fresh)
            {
                copiedContext=null; // standalone clipboard text must never acquire a game paste target later
                var standalone=Router.StandaloneDraft(new(text,TranscriptionStatus.Success), useClassicDefaults() ? ClassicChatDefaults.DraftByteLimit : 4096);
                if(standalone.Valid) detachedTranscript=text;
                return new(standalone.Message,standalone.ClipboardText,standalone.Valid ? "Say (standalone)" : "Unconfirmed",
                    standalone.Explanation,timing.Elapsed.TotalMilliseconds);
            }
            if(context!.VerifiedPrefixes.Count==0 || context.MessageLimit<=0)
            {
                if(allowUnverifiedSayDrafts())
                {
                    // The opt-in draft pins Say; a later paste must find focused Say chat in this same session.
                    copiedContext=context with {ActiveDestination=Destination.Say,ActiveChannelId=null,ActivePanelUnsupported=false};
                    var (temporaryDecision, preview) = Router.SetupSkippedDraft(new(text, TranscriptionStatus.Success), context);
                    string temporaryAudience = temporaryDecision.ChannelId is int temporaryId ? $"{temporaryDecision.ChannelName} (/{temporaryId}, setup skipped)" : "Say (setup skipped)";
                    return new(preview.Message, preview.ClipboardText, preview.Valid ? temporaryAudience : "Unconfirmed",
                        preview.Valid ? "Chat compatibility setup skipped. " + (temporaryDecision.ChannelId is not null ? "Explicit joined channel selected. " : "Say draft copied. ") +
                            "Click the stick to open Say chat and paste, then press A to send. Prefix and game length limit are unverified." : preview.Explanation,
                        timing.Elapsed.TotalMilliseconds);
                }
                string setup=context.VerifiedPrefixes.Count==0
                    ? "Addon detected. Finish the in-game setup: /wvr rendered, then /wvr verify say after testing Say."
                    : "Addon detected; chat prefixes are confirmed.";
                if(context.MessageLimit<=0) setup+=" Record the tested chat limit with /wvr limit bytes <limit>.";
                return new(text,null,"Setup required",setup+" Clipboard retained.",timing.Elapsed.TotalMilliseconds);
            }
            var scores=new Dictionary<Destination,double>();
            if(handle!=0 && fresh)
            {
                byte[] output=new byte[2048];int count=wvr_predict(handle,textOnlyFeatures ? Features.MessageOnly(text) : Features.Encode(text,context),output,output.Length);
                if(count<0) return new(text,null,"Unconfirmed","Classifier failed; clipboard retained.",timing.Elapsed.TotalMilliseconds);
                foreach(string line in Encoding.UTF8.GetString(output,0,count).Split('\n',StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts=line.Split('\t');
                    if(parts.Length==2 && Enum.TryParse<Destination>(parts[0].Replace("__label__",""),true,out var destination) && double.TryParse(parts[1],NumberStyles.Float,CultureInfo.InvariantCulture,out var score))
                        scores[destination]=score;
                }
            }
            if(textOnlyFeatures && !Features.HasGuildAddress(text)) scores.Remove(Destination.Guild);
            var decision=router.Decide(new(text,TranscriptionStatus.Success),context,fresh,scores);
            var draft=Router.Draft(decision.Message,decision,context,fresh);
            string audience=decision.ChannelId is int id ? $"{decision.ChannelName} (/{id})" : decision.Destination?.ToString() ?? "Unconfirmed";
            return new(decision.Message,draft.ClipboardText,audience,draft.Explanation+
                (context.UsesClassicDefaults ? " Classic chat defaults; app cap " + context.MessageLimit + " UTF-8 bytes." : "")+
                (modelError.Length>0 ? " Classifier unavailable: "+modelError : ""),timing.Elapsed.TotalMilliseconds);
        }
    }
    private RoutedDraft PrepareForAddon(string text)
    {
        var timing=Stopwatch.StartNew();
        text=text.Trim();
        if(AddonEnvelope.ValidateMessage(text) is { } error) return new(text,null,"Needs editing",error,0);
        string hint="default";
        if(handle!=0 && textOnlyFeatures)
        {
            byte[] output=new byte[2048];
            int count=wvr_predict(handle,Features.MessageOnly(text),output,output.Length);
            if(count>=0)
            {
                var scores=new List<(Destination Kind,double Score)>();
                foreach(string line in Encoding.UTF8.GetString(output,0,count).Split('\n',StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts=line.Split('\t');
                    if(parts.Length==2 && Enum.TryParse<Destination>(parts[0].Replace("__label__",""),true,out var destination) &&
                        double.TryParse(parts[1],NumberStyles.Float,CultureInfo.InvariantCulture,out var score) && double.IsFinite(score) && score is >=0 and <=1)
                        scores.Add((destination,score));
                }
                var ranked=scores.OrderByDescending(s=>s.Score).ToArray();
                if(ranked.Length>0)
                {
                    var best=ranked[0]; bool publicRoute=best.Kind is Destination.General or Destination.Trade or Destination.LookingForGroup;
                    double threshold=publicRoute ? intentPolicy.PublicThreshold : intentPolicy.GuildThreshold;
                    if((best.Kind==Destination.Guild && Features.HasGuildAddress(text) || publicRoute && intentPolicy.PublicValidated) && best.Score>=threshold &&
                        best.Score-(ranked.Length>1 ? ranked[1].Score : 0)>=intentPolicy.Margin)
                        hint="i:"+best.Kind.ToString().ToLowerInvariant();
                }
            }
        }
        return new(text,text,hint=="default" ? "Auto (addon)" : hint[2..]+" (suggested)",
            "Addon resolves explicit instructions, current groups and joined channels on the final click. No screen capture or setup. Search fields receive plain text."+
            (modelError.Length>0 ? " Classifier unavailable; addon defaults remain available." : ""),timing.Elapsed.TotalMilliseconds) {AddonHint=hint};
    }

    public RoutedDraft Confirm(string text, string prefix, int verifiedLimit, bool bytes)
    {
        lock(gate)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            if(UsesAddonRouting)
            {
                string? hint=prefix switch {"/say" or "/s"=>"m:say","/g"=>"m:guild","/p"=>"m:party","/raid" or "/ra"=>"m:raid","/i"=>"m:instance",_=>null};
                if(hint is null && int.TryParse(prefix.TrimStart('/'),out int channel) && channel is >0 and <=9999) hint="m:channel:"+channel;
                string? error=AddonEnvelope.ValidateMessage(text);
                return new(text,error is null && hint is not null ? text : null,prefix,error ?? (hint is null ? "Invalid destination." : "Manual destination; addon checks availability at paste."),0) {AddonHint=hint ?? "default"};
            }
            detachedTranscript=null;
            copiedContext=tracker.Current;
            var context=tracker.Current;bool fresh=tracker.IsFresh(clock.Elapsed);
            if(fresh && context?.FocusedText is not null)
                return new(text,null,"Unconfirmed","A text field is focused. Return to chat before choosing a chat destination.",0);
            var destination=prefix.ToLowerInvariant() switch
            {
                "/say" or "/s" => Destination.Say, "/g" or "/guild" => Destination.Guild,
                "/p" or "/party" => Destination.Party, "/raid" or "/ra" => Destination.Raid,
                "/i" or "/instance" => Destination.Instance, _ => Destination.Custom
            };
            int? id=int.TryParse(prefix.TrimStart('/'),out int number) ? number : null;
            if(destination==Destination.Custom && id is int channelId && fresh)
                destination=context!.Channels.FirstOrDefault(c=>c.Id==channelId)?.Kind ?? Destination.Custom;
            var decision=new RouteDecision(destination,id,new Dictionary<Destination,double>(),RouteReason.ManualCorrection,
                "Manually confirmed destination.",text);
            var draft=Router.Draft(text,decision,context,fresh,true,prefix,verifiedLimit,bytes);
            return new(text,draft.ClipboardText,prefix,draft.Explanation,0);
        }
    }
    /// <summary>Rechecks focus and the context used to prepare the copied draft.</summary>
    public string? ValidatePaste()
    {
        Poll();
        lock(gate)
        {
            if(!WindowsCapture.IsGameForeground(Settings.Load())) return "Return to the game before pasting.";
            return PasteContext.Validate(copiedContext,tracker.Current,tracker.IsFresh(clock.Elapsed));
        }
    }

    /// <summary>Validates opening a closed chat and confirms focus before the separate paste request.</summary>
    public (string? Error, GameContext? Context) InspectOpenPaste(bool allowClosed, bool opening)
    {
        Poll();
        lock(gate)
        {
            var current = tracker.Current;
            var expected = copiedContext;
            if (!WindowsCapture.IsGameForeground(Settings.Load())) return ("Return to the game before pasting.", current);
            return (PasteContext.ValidateOpenPaste(expected, current, tracker.IsFresh(clock.Elapsed), allowClosed, opening), current);
        }
    }

    public (string? Error, GameContext? Context) SubmissionContext(bool allowClosed)
    {
        Poll();
        lock(gate)
        {
            var current = tracker.Current;
            var original = copiedContext;
            if (!WindowsCapture.IsGameForeground(Settings.Load())) return ("Return to the game before submitting.", current);
            if (current is null || original is null) return ("Addon context is not connected. " + captureError, current);
            if (current.FocusedText is not null) return ("Auto-send is limited to chat. Search fields use paste and manual confirmation.", current);
            if (current.ActivePanelUnsupported) return ("Unsupported chat audience.", current);
            // The pasted command can intentionally change the audience. The final echo gate verifies the requested audience.
            var expected = original with { ActiveDestination = current.ActiveDestination, ActiveChannelId = current.ActiveChannelId };
            var check = allowClosed && current.ChatInput == ChatInputState.Closed ? current with { ChatInput = ChatInputState.Open } : current;
            return (PasteContext.Validate(expected, check, tracker.IsFresh(clock.Elapsed)), current);
        }
    }

    public void Dispose()
    {
        timer?.Dispose();
        lock(gate) {disposed=true;if(handle!=0){wvr_destroy(handle);handle=0;}}
    }
}
