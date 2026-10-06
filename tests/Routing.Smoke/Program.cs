using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using SpeakForever;
using SpeakForever.Configuration;
using SpeakForever.Routing;
using SpeakForever.Speech;
using VoiceRouter.Core;

if(args.Length==1 && args[0] is "--context-probe" or "--calibrate-context" or "--auto-context-probe" or "--watch-context")
{
    try
    {
        var settings=VoiceRouter.App.Settings.Load();
        if(args[0]=="--watch-context")
        {
            for(int attempt=0;attempt<120;attempt++)
            {
                if(VoiceRouter.App.WindowsCapture.IsGameForeground(settings))
                {
                    try
                    {
                        var observedContext=VoiceRouter.App.WindowsCapture.ReadAuto(settings);
                        Thread.Sleep(350);
                        var nextContext=VoiceRouter.App.WindowsCapture.ReadAuto(settings);
                        if(nextContext.Session!=observedContext.Session || nextContext.Heartbeat==observedContext.Heartbeat)
                            throw new IOException("No stable advancing addon heartbeat observed.");
                        Console.WriteLine($"Live observedContext: protocol {nextContext.ProtocolVersion}, build {nextContext.ClientBuild}, heartbeat {observedContext.Heartbeat} -> {nextContext.Heartbeat}, group {nextContext.Group}, guild {nextContext.InGuild}, chat {nextContext.ChatInput}, audience {nextContext.ActiveDestination}, channels {string.Join(",",nextContext.Channels.Select(c=>$"{c.Id}:{c.Kind}"))}.");
                        return;
                    }
                    catch(Exception error) {if(attempt%10==0) Console.WriteLine("Foreground capture: "+error.Message);}
                }
                Thread.Sleep(500);
            }
            throw new IOException("No valid foreground game context observed within 60 seconds.");
        }
        if(args[0]=="--calibrate-context") settings=VoiceRouter.App.WindowsCapture.Calibrate(settings);
        var first=args[0]=="--auto-context-probe" ? VoiceRouter.App.WindowsCapture.ReadAuto(settings) : VoiceRouter.App.WindowsCapture.Read(settings);
        Thread.Sleep(350);
        var second=args[0]=="--auto-context-probe" ? VoiceRouter.App.WindowsCapture.ReadAuto(settings) : VoiceRouter.App.WindowsCapture.Read(settings);
        Console.WriteLine($"Capture: ({settings.StripX},{settings.StripY}), pitch {settings.CellPixels}; build {second.ClientBuild}; heartbeat {first.Heartbeat} -> {second.Heartbeat}.");
        Console.WriteLine($"Prefixes: {string.Join(", ",second.VerifiedPrefixes.Select(p=>$"{p.Key}={p.Value}"))}; message limit: {second.MessageLimit}; chat input: {second.ChatInput}.");
        if(first.Session==second.Session && first.Heartbeat==second.Heartbeat) throw new IOException("Heartbeat is not advancing.");
    }
    catch(Exception error) {Console.Error.WriteLine("Context probe failed: "+error.Message); Environment.ExitCode=1;}
    return;
}
string package=Path.GetFullPath(args[0]);
NativeLibrary.SetDllImportResolver(typeof(DraftRouter).Assembly,(name,assembly,path)=>
    name=="voice_router_native" ? NativeLibrary.Load(Path.Combine(package,"voice_router_native.dll")) : 0);
var prefixes=new Dictionary<Destination,string>{{Destination.Say,"/say"},{Destination.Guild,"/g"},{Destination.Party,"/p"},
    {Destination.Raid,"/raid"},{Destination.Instance,"/i"},{Destination.Custom,"/number"}};
GameContext context=new(2,"test-client",1,1,GroupCategory.Party,true,[new(5,"Trade",Destination.Trade)],prefixes,255,true);
bool missing=false; int checks=0;
void Check(bool condition,string name) {if(!condition) throw new Exception(name); checks++;}
using(var local=new DraftRouter(Path.Combine(package,"models")))
{
    Check(local.UsesAddonRouting && local.ClassifierLoaded,"Default app uses real classifier without capture bridge");
    Check(local.Prepare("In General, hello friends").ClipboardText=="In General, hello friends","Explicit directive transported intact to addon");
    Check(local.Prepare("Guildies who wants to join our guild event?").AddonHint=="i:guild","Text-only Guild address hint");
    Check(local.Prepare("I mentioned the guild yesterday").AddonHint=="default","Guild reference is not addressed audience");
    Check(local.Prepare("Hello friends").AddonHint=="default","Ordinary greeting never becomes Guild from friends keyword");
    Check(local.Prepare("where do I learn skinning in this zone").AddonHint=="i:general","Actual General model hint is enabled by default");
    Check(local.Prepare("selling iron bars").AddonHint=="i:trade","Actual Trade model hint is enabled by default");
    Check(local.Prepare("LFM one healer for Deadmines").AddonHint=="i:lookingforgroup","Actual LFG model hint is enabled by default");
    Check(!local.Prepare(new string('é',101)).Ready,"Transport UTF8 limit");
}
using(var router=new DraftRouter(Path.Combine(package,"models"),()=>missing ? throw new IOException("Missing game") : context,()=>false))
{
    Check(router.ChatDraftRecordingAvailable,"Fresh chat context permits recording before opening chat");
    Check(router.ClassifierLoaded,"Real fastText model loaded without another Whisper instance");
    Check(router.Prepare("Hello friends").ClipboardText=="/p Hello friends","Party default while grouped");
    Check(router.Prepare("Tell everyone around me we need help").ClipboardText=="/say we need help","Explicit local audience");
    Check(router.Prepare("Ask in trade selling potions").ClipboardText=="/5 selling potions","Explicit current Trade ID");
    Check(router.Prepare("how's the guild doing guys?").Destination=="Guild","Real classifier guild address");
    context=context with {Heartbeat=2,Channels=[new(2,"Trade",Destination.Trade)]};router.RefreshContext();
    Check(router.Prepare("Ask in trade selling potions").ClipboardText=="/2 selling potions","Renumbered public channel");
    context=context with {Heartbeat=3,Group=GroupCategory.Instance};router.RefreshContext();
    Check(router.Prepare("Hello friends").ClipboardText=="/i Hello friends","Instance transition updates group default");
    Check(!router.Prepare(new string('a',256)).Ready,"Oversized draft retained");
    Check(!router.Prepare("/logout").Ready,"Leading slash blocked");
    Check(!router.Prepare(" ").Ready,"Silence cannot copy");
    Check(router.Prepare("Hello 世界").ClipboardText=="/i Hello 世界","Unicode preserved");
    context=context with {Heartbeat=4,FocusedText=new("Auction House search",TextFieldKind.AuctionHouse,63,false)};router.RefreshContext();
    Check(!router.ChatDraftRecordingAvailable && router.FocusedFieldAvailable,"Verified search uses focused-field recording path");
    Check(router.Prepare("Runecloth").ClipboardText=="Runecloth","AH plain text, no chat prefix");
    Check(!router.Prepare(new string('a',64)).Ready,"AH limit");
    missing=true;router.RefreshContext();
    Check(!router.ChatDraftRecordingAvailable,"Stale search context cannot become chat recording");
    Check(!router.Prepare("Hello friends").Ready,"Missing context cannot copy");
    Check(router.Confirm("Hello friends","/say",255,true).ClipboardText=="/say Hello friends","Manual confirmed route");
    Check(!router.Confirm("Hello friends","/logout",255,true).Ready,"Manual invalid command rejected");
    missing=false;context=context with {Heartbeat=5,FocusedText=null};router.RefreshContext();
    missing=true;router.RefreshContext();
    Check(router.Prepare("Hello friends").ClipboardText=="/say Hello friends","Missing chat context uses standalone Say");
    Check(!router.Prepare("Tell guild hello").Ready,"Missing context retains explicit audience");
    missing=false;router.RefreshContext();
    Thread.Sleep(2100);
    Check(router.Prepare("Hello friends").ClipboardText=="/say Hello friends","Stale chat context uses standalone Say");
    var times=new List<double>();
    for(uint i=6;i<106;i++) {context=context with {Heartbeat=i};router.RefreshContext();times.Add(router.Prepare("Hello friends").RoutingMilliseconds);}
    context=context with {Heartbeat=106,VerifiedPrefixes=new Dictionary<Destination,string>(),MessageLimit=0};router.RefreshContext();
    var setup=router.Prepare("Hello friends");
    Check(!setup.Ready && setup.Destination=="Setup required" && setup.Reason.Contains("/wvr rendered"),"Detected unconfigured addon gives setup instructions");
    Console.WriteLine($"Routing checks: {checks}; mean {times.Average():F3} ms; max {times.Max():F3} ms (capture excluded).");
}
using(var previewRouter=new DraftRouter(Path.Combine(package,"models"),()=>context,()=>true))
{
    Check(previewRouter.Prepare("Hello friends").ClipboardText=="/say Hello friends","Unverified setup opt-in copies Say preview");
    Check(previewRouter.Prepare("In trade, selling linen").ClipboardText=="/2 selling linen","Setup skip honors explicit joined Trade channel");
    Check(!previewRouter.Prepare("In general, hello").Ready,"Setup skip blocks unjoined General instead of Say fallback");
    Check(!previewRouter.Prepare("Tell guild hello").Ready,"Setup skip does not redirect explicit unavailable audience");
    Check(!previewRouter.Prepare("/logout").Ready,"Setup skip blocks leading slash");
    Check(!previewRouter.Prepare(" ").Ready,"Setup skip preserves clipboard on silence");
}
using(var classicRouter=new DraftRouter(Path.Combine(package,"models"),()=>context,()=>false,()=>true))
{
    Check(classicRouter.Prepare("In trade, selling cloth").ClipboardText=="/2 selling cloth","Classic defaults route explicit current Trade without setup");
    Check(classicRouter.Prepare("Hello friends").Destination=="Instance","Classic defaults route current group without setup skipped label");
    Check(!classicRouter.Prepare(new string('x',201)).Ready,"Classic draft cap rejects oversized text");
}
Check(ModelCatalog.All.Count==1 && ModelCatalog.All[0].Name=="Turbo","Only Turbo offered");
missing=true;
using(var recovering=new DraftRouter(Path.Combine(package,"models"),()=>missing ? throw new IOException("Waiting for addon") : context,()=>false,()=>true))
{
    Check(recovering.Prepare("Hello friends").Destination=="Say (standalone)","Startup without context retains a standalone draft");
    Check(!recovering.CanRecoverDetachedDraft,"No recovery before fresh context");
    missing=false;context=context with {Heartbeat=context.Heartbeat+1};recovering.RefreshContext();
    Check(recovering.CanRecoverDetachedDraft,"Loaded addon automatically permits detached draft recovery");
    Check(recovering.RecoverDetachedDraft().ClipboardText=="/i Hello friends","Final click can reroute recovered draft to actual group");
    Check(recovering.CanRecoverDetachedDraft,"Clipboard failure can retry detached recovery");
    recovering.CompleteDetachedRecovery();
    Check(!recovering.CanRecoverDetachedDraft,"Successfully recopied draft no longer detached");
    missing=true;recovering.RefreshContext();
    Check(!recovering.Prepare("In general, hello").Ready,"Missing context does not make an explicit General request a Say draft");
}
Check(!new Config().CheckForUpdates && new Config().MaxSeconds==30 && !new Config().FinishOnPause,"Fork defaults");
if(args.Length>1)
{
    using var input=new BinaryReader(File.OpenRead(args[1]));
    input.BaseStream.Position=12;
    byte[]? pcm=null;
    while(input.BaseStream.Position+8<=input.BaseStream.Length)
    {
        string chunk=new string(input.ReadChars(4));int length=input.ReadInt32();
        if(chunk=="fmt ") {byte[] fmt=input.ReadBytes(length);Check(BitConverter.ToInt16(fmt)==1 && BitConverter.ToInt16(fmt,2)==1 && BitConverter.ToInt32(fmt,4)==16000 && BitConverter.ToInt16(fmt,14)==16,"PCM input");}
        else if(chunk=="data") {pcm=input.ReadBytes(Math.Min(length,3*16000*2));break;}
        else input.BaseStream.Position+=length+(length&1);
    }
    Check(pcm is not null,"Audio data present");
    float[] audio=new float[pcm!.Length/2];for(int i=0;i<audio.Length;i++) audio[i]=BitConverter.ToInt16(pcm,i*2)/32768f;
    await using var engine=new Engine(new Config());
    var load=Stopwatch.StartNew();await engine.LoadModelAsync(Path.Combine(package,"models","ggml-large-v3-turbo-q5_0.bin"));
    Check(engine.LoadedModel is not null,engine.ModelStatus);
    var result=await engine.TranscribeAsync(audio);Check(result.Text.Length>0,"Turbo real speech");Array.Clear(audio);
    Console.WriteLine(JsonSerializer.Serialize(new {checks,model="Turbo q5_0",runtime=Transcriber.RuntimeInfo,load_seconds=load.Elapsed.TotalSeconds-result.Took.TotalSeconds,
        recognition_seconds=result.Took.TotalSeconds,peak_process_mib=Process.GetCurrentProcess().PeakWorkingSet64/1048576.0,
        microphone_release_to_clipboard="Not measured",gameplay_impact="Not measured"}));
}
