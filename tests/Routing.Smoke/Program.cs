using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using SpeakForever;
using SpeakForever.Configuration;
using SpeakForever.Routing;
using SpeakForever.Speech;
using VoiceRouter.Core;

string package=Path.GetFullPath(args[0]);
NativeLibrary.SetDllImportResolver(typeof(DraftRouter).Assembly,(name,assembly,path)=>
    name=="voice_router_native" ? NativeLibrary.Load(Path.Combine(package,"voice_router_native.dll")) : 0);
var prefixes=new Dictionary<Destination,string>{{Destination.Say,"/say"},{Destination.Guild,"/g"},{Destination.Party,"/p"},
    {Destination.Raid,"/raid"},{Destination.Instance,"/i"},{Destination.Custom,"/number"}};
GameContext context=new(2,"test-client",1,1,GroupCategory.Party,true,[new(5,"Trade",Destination.Trade)],prefixes,255,true);
bool missing=false; int checks=0;
void Check(bool condition,string name) {if(!condition) throw new Exception(name); checks++;}
using(var router=new DraftRouter(Path.Combine(package,"models"),()=>missing ? throw new IOException("Missing game") : context))
{
    Check(router.ClassifierLoaded,"Real fastText model loaded without another Whisper instance");
    Check(router.Prepare("Hello friends").ClipboardText=="/say Hello friends","Say default while grouped");
    Check(router.Prepare("Tell everyone around me we need help").ClipboardText=="/say we need help","Explicit local audience");
    Check(router.Prepare("Ask in trade selling potions").ClipboardText=="/5 selling potions","Explicit current Trade ID");
    Check(router.Prepare("how's the guild doing guys?").Destination=="Guild","Real classifier guild address");
    context=context with {Heartbeat=2,Channels=[new(2,"Trade",Destination.Trade)]};router.RefreshContext();
    Check(router.Prepare("Ask in trade selling potions").ClipboardText=="/2 selling potions","Renumbered public channel");
    context=context with {Heartbeat=3,Group=GroupCategory.Instance};router.RefreshContext();
    Check(router.Prepare("Hello friends").ClipboardText=="/say Hello friends","Instance transition keeps Say default");
    Check(!router.Prepare(new string('a',256)).Ready,"Oversized draft retained");
    Check(!router.Prepare("/logout").Ready,"Leading slash blocked");
    Check(!router.Prepare(" ").Ready,"Silence cannot copy");
    Check(router.Prepare("Hello 世界").ClipboardText=="/say Hello 世界","Unicode preserved");
    context=context with {Heartbeat=4,FocusedText=new("Auction House search",TextFieldKind.AuctionHouse,63,false)};router.RefreshContext();
    Check(router.Prepare("Runecloth").ClipboardText=="Runecloth","AH plain text, no chat prefix");
    Check(!router.Prepare(new string('a',64)).Ready,"AH limit");
    missing=true;router.RefreshContext();
    Check(!router.Prepare("Hello friends").Ready,"Missing context cannot copy");
    Check(router.Confirm("Hello friends","/say",255,true).ClipboardText=="/say Hello friends","Manual confirmed route");
    Check(!router.Confirm("Hello friends","/logout",255,true).Ready,"Manual invalid command rejected");
    missing=false;context=context with {Heartbeat=5,FocusedText=null};router.RefreshContext();
    Thread.Sleep(2100);
    Check(!router.Prepare("Hello friends").Ready,"Repeated heartbeat goes stale");
    var times=new List<double>();
    for(uint i=6;i<106;i++) {context=context with {Heartbeat=i};router.RefreshContext();times.Add(router.Prepare("Hello friends").RoutingMilliseconds);}
    Console.WriteLine($"Routing checks: {checks}; mean {times.Average():F3} ms; max {times.Max():F3} ms (capture excluded).");
}
Check(ModelCatalog.All.Count==1 && ModelCatalog.All[0].Name=="Turbo","Only Turbo offered");
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
