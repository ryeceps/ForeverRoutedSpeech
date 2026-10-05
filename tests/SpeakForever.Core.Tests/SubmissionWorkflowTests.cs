using System.Text;
using System.Buffers.Binary;
using SpeakForever.Dictation;
using SpeakForever.Routing;
using VoiceRouter.Core;

namespace SpeakForever.Core.Tests;
public sealed class SubmissionWorkflowTests
{
    static readonly RoutedDraft Draft=new("Hello","/say Hello","Say","test",0);
    static GameContext Context(string text="",ChatInputState input=ChatInputState.Open)=>new(4,"fixture",1,1,
        GroupCategory.Solo,false,[],new Dictionary<Destination,string>(),0,true,input,null,Destination.Say,null,false,
        Encoding.UTF8.GetByteCount(text),StatusProtocol.Checksum(Encoding.UTF8.GetBytes(text)));
    static Task NoDelay(CancellationToken ct)=>Task.CompletedTask;

    [Fact]
    public async Task ClosedChatOpensPastesAndSubmitsOnlyAfterReadback()
    {
        var context=Context(input:ChatInputState.Closed);var actions=new List<SubmissionInput>();
        var result=await SubmissionWorkflow.RunAsync(Draft,_=>(null,context),action=>
        {
            actions.Add(action);
            if(action==SubmissionInput.OpenChat) context=Context();
            if(action==SubmissionInput.Paste) context=Context("Hello");
            if(action==SubmissionInput.Submit) context=Context(input:ChatInputState.Closed) with { Heartbeat=2 };
            return(null,true);
        },CancellationToken.None,NoDelay);
        Assert.Null(result.Error);
        Assert.Equal([SubmissionInput.OpenChat,SubmissionInput.Paste,SubmissionInput.Submit],actions);
    }
    [Fact]
    public async Task PartialPasteNeverSubmitsOrRetries()
    {
        var actions=new List<SubmissionInput>();
        var result=await SubmissionWorkflow.RunAsync(Draft,_=>(null,Context()),action=>
        {actions.Add(action);return("Partial input",true);},CancellationToken.None,NoDelay);
        Assert.NotNull(result.Error);Assert.True(result.Attempted);
        Assert.Equal([SubmissionInput.Paste],actions);
    }
    [Fact]
    public async Task MismatchedTextNeverSubmits()
    {
        bool pasted=false;var actions=new List<SubmissionInput>();
        var result=await SubmissionWorkflow.RunAsync(Draft,_=>(null,Context(pasted ? "Something else" : "")),action=>
        {actions.Add(action);pasted=true;return(null,true);},CancellationToken.None,NoDelay);
        Assert.NotNull(result.Error);Assert.Equal([SubmissionInput.Paste],actions);
    }
    [Fact]
    public async Task ExistingTextIsNotOverwritten()
    {
        var actions=new List<SubmissionInput>();
        var result=await SubmissionWorkflow.RunAsync(Draft,_=>(null,Context("Already typed")),action=>
        {actions.Add(action);return(null,true);},CancellationToken.None,NoDelay);
        Assert.NotNull(result.Error);Assert.Empty(actions);
    }
    [Fact]
    public async Task FocusChangeAfterPasteStopsSubmission()
    {
        bool pasted=false;var actions=new List<SubmissionInput>();
        var result=await SubmissionWorkflow.RunAsync(Draft,_=>pasted ? ("Focus changed",null) : (null,Context()),action=>
        {actions.Add(action);pasted=true;return(null,true);},CancellationToken.None,NoDelay);
        Assert.Equal("Focus changed",result.Error);Assert.Equal([SubmissionInput.Paste],actions);
    }
    [Fact]
    public async Task CancelledOrOldAddonRequestsNoInput()
    {
        foreach(bool cancelled in new[]{false,true})
        {
            using var cts=new CancellationTokenSource();if(cancelled) cts.Cancel();
            var actions=new List<SubmissionInput>();
            var result=await SubmissionWorkflow.RunAsync(Draft,_=>(null,Context() with {ProtocolVersion=3}),action=>
            {actions.Add(action);return(null,true);},cts.Token,NoDelay);
            Assert.NotNull(result.Error);Assert.Empty(actions);
        }
    }

    [Fact]
    public void AutoSendDefaultsOffAndRequiresMatchingAudienceAndUnicodeText()
    {
        Assert.False(new SpeakForever.Configuration.Config().AutoSubmit);
        var context = Context("Hello 世界") with { ActiveDestination = Destination.General, ActiveChannelId = 1 };
        Assert.Null(SubmissionGate.ReadyToSubmit("/1 Hello 世界", "Hello 世界", context));
        Assert.NotNull(SubmissionGate.ReadyToSubmit("/2 Hello 世界", "Hello 世界", context));
        Assert.NotNull(SubmissionGate.ReadyToSubmit("/say Hello 世界", "Hello 世界", context));
        Assert.NotNull(SubmissionGate.ReadyToSubmit("/1 Other text", "Other text", context));
        Assert.False(SubmissionGate.Matches(context with { InputChecksum = null }, "Hello 世界"));
    }

    [Fact]
    public async Task ReadbackTimeoutAndCancellationAfterPasteNeverSend()
    {
        foreach (bool cancelAfterPaste in new[] { false, true })
        {
            using var cancel = new CancellationTokenSource();
            var actions = new List<SubmissionInput>();
            var result = await SubmissionWorkflow.RunAsync(Draft, _ => (null, Context()), action =>
            {
                actions.Add(action);
                if (cancelAfterPaste) cancel.Cancel();
                return (null, true);
            }, cancel.Token, NoDelay);
            Assert.NotNull(result.Error);
            Assert.Equal([SubmissionInput.Paste], actions);
        }
    }

    [Fact]
    public void VersionFourDecodesUnicodeTextEchoAndRejectsMalformedEcho()
    {
        byte[] Frame(string echo)
        {
            byte[] payload = Encoding.UTF8.GetBytes("4\tfixture\tsolo\t0\t0\tbytes\t\t\topen\tnone\t\t0\tchars\tSay\t\t" + echo);
            byte[] frame = new byte[512];
            "WVR1"u8.CopyTo(frame);
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(4), (ushort)payload.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(6), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(10), 1);
            payload.CopyTo(frame, 14);
            BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(14 + payload.Length), StatusProtocol.Checksum(frame.AsSpan(0, 14 + payload.Length)));
            return frame;
        }
        byte[] text = Encoding.UTF8.GetBytes("Hello 世界");
        var decoded = StatusProtocol.Decode(Frame($"{text.Length}\t{StatusProtocol.Checksum(text)}"));
        Assert.Equal(4, decoded.ProtocolVersion);
        Assert.True(SubmissionGate.Matches(decoded, "Hello 世界"));
        Assert.Null(StatusProtocol.Decode(Frame("-1\t")).InputBytes);
        Assert.Throws<FormatException>(() => StatusProtocol.Decode(Frame("-2\t0")));
        Assert.Throws<FormatException>(() => StatusProtocol.Decode(Frame("-1\t5")));
    }

    [Fact]
    public async Task LeftStickIsReservedForCancellation()
    {
        await using var engine = TestSetup.NewEngine();
        var reason = await engine.SetBindingAsync(BindingKind.Dictate, SpeakForever.Input.Chord.Parse("LS"), TestContext.Current.CancellationToken);
        Assert.Contains("cancelling dictation", reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancelWhileOpeningPreventsPasteAndSubmission()
    {
        using var cancel = new CancellationTokenSource();
        var actions = new List<SubmissionInput>();
        var result = await SubmissionWorkflow.RunAsync(Draft,
            _ => (null, Context(input: ChatInputState.Closed)), action =>
            {
                actions.Add(action);
                cancel.Cancel();
                return (null, true);
            }, cancel.Token, NoDelay);
        Assert.NotNull(result.Error);
        Assert.Equal([SubmissionInput.OpenChat], actions);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SubmitClosesNaturallyOrEscapesEmptyChatExactlyOnce(bool closesNaturally)
    {
        var context = Context();
        var actions = new List<SubmissionInput>();
        var result = await SubmissionWorkflow.RunAsync(Draft, _ => (null, context), action =>
        {
            actions.Add(action);
            if (action == SubmissionInput.Paste) context = Context("Hello");
            if (action == SubmissionInput.Submit) context = Context(input: closesNaturally ? ChatInputState.Closed : ChatInputState.Open) with { Heartbeat = 2 };
            if (action == SubmissionInput.CloseChat) context = Context(input: ChatInputState.Closed) with { Heartbeat = 3 };
            return (null, true);
        }, CancellationToken.None, NoDelay);
        Assert.Null(result.Error);
        Assert.Equal(closesNaturally ? [SubmissionInput.Paste, SubmissionInput.Submit] :
            new[] { SubmissionInput.Paste, SubmissionInput.Submit, SubmissionInput.CloseChat }, actions);
    }

    [Fact]
    public async Task RemainingTextAfterSubmitIsNotDiscardedOrResent()
    {
        var context = Context();
        var actions = new List<SubmissionInput>();
        var result = await SubmissionWorkflow.RunAsync(Draft, _ => (null, context), action =>
        {
            actions.Add(action);
            if (action == SubmissionInput.Paste) context = Context("Hello");
            if (action == SubmissionInput.Submit) context = Context("Still here") with { Heartbeat = 2 };
            return (null, true);
        }, CancellationToken.None, NoDelay);
        Assert.NotNull(result.Error);
        Assert.Equal([SubmissionInput.Paste, SubmissionInput.Submit], actions);
    }

    [Fact]
    public async Task FocusChangeAfterSubmitRequestsNoEscape()
    {
        var context = Context();
        bool submitted = false;
        var actions = new List<SubmissionInput>();
        var result = await SubmissionWorkflow.RunAsync(Draft, _ => submitted ? ("focus changed", null) : (null, context), action =>
        {
            actions.Add(action);
            if (action == SubmissionInput.Paste) context = Context("Hello");
            if (action == SubmissionInput.Submit) submitted = true;
            return (null, true);
        }, CancellationToken.None, NoDelay);
        Assert.NotNull(result.Error);
        Assert.Equal([SubmissionInput.Paste, SubmissionInput.Submit], actions);
    }
}
