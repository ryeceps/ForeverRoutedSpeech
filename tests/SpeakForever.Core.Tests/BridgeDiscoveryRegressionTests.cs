using System.Text.Json;
using VoiceRouter.Core;

namespace SpeakForever.Core.Tests;

public class BridgeDiscoveryRegressionTests
{
    public sealed record AddonFixture(int Height, double UiScale, double AddonScale, string Frame);
    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive=true };
    static AddonFixture[] Fixtures() => JsonSerializer.Deserialize<AddonFixture[]>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"addon-bridge-geometry.json")),
        JsonOptions)!;

    static BridgeMatch? Locate(AddonFixture fixture, double? overridePitch=null, bool corrupt=false, bool occluded=false)
    {
        byte[] bytes=Convert.FromBase64String(fixture.Frame);
        if (corrupt) bytes[24]^=1;
        // Model WoW's normalized UI coordinates, including the actual Lua frame scale.
        double pitch=overridePitch ?? fixture.AddonScale*fixture.UiScale*fixture.Height/768;
        int left=23,top=13;
        return BridgeLocator.Find(512,96,(x,y)=>
        {
            if (x<left || y<top || x>=left+128*pitch || y>=top+32*pitch)
                return ((byte)17,(byte)39,(byte)24); // Scene colors reported in the failure log.
            int column=(int)((x-left+.5)/pitch),row=(int)((y-top+.5)/pitch);
            if (column>=128 || row>=32) return ((byte)17,(byte)39,(byte)24);
            if (occluded && row>=2 && row<=5) return ((byte)73,(byte)37,(byte)131);
            int bit=row*128+column;
            byte value=((bytes[bit/8]>>(bit%8))&1)==1 ? (byte)255 : (byte)0;
            return (value,value,value);
        });
    }

    [Fact]
    public void ActualLuaOutputIsFoundAtEveryResolutionAndUiScale()
    {
        foreach (var fixture in Fixtures())
        {
            var found=Locate(fixture);
            Assert.NotNull(found);
            Assert.Equal(23,found.X);
            Assert.Equal(13,found.Y);
            Assert.Equal(1,found.Pitch);
            var expected=StatusProtocol.Decode(Convert.FromBase64String(fixture.Frame));
            Assert.Equal(expected.Session,found.Context.Session);
            Assert.Equal(expected.Heartbeat,found.Context.Heartbeat);
            Assert.Equal(expected.Group,found.Context.Group);
            Assert.Equal(expected.Channels,found.Context.Channels);
        }
    }

    [Fact]
    public void PreviousScaleFormulaReproducesMissingConnectionAt1200PixelsHigh()
    {
        var fixture=Fixtures().First(f=>f.Height==1200 && f.UiScale==.75);
        double previousScale=1/fixture.UiScale;
        double previousPitch=previousScale*fixture.UiScale*fixture.Height/768;
        Assert.Equal(1.5625,previousPitch);
        Assert.Null(Locate(fixture,previousPitch));
        Assert.NotNull(Locate(fixture));
    }

    [Fact]
    public void DamagedOrCoveredSignalCannotBecomeConnectedContext()
    {
        var fixture=Fixtures()[0];
        Assert.Null(Locate(fixture,corrupt:true));
        Assert.Null(Locate(fixture,occluded:true));
        Assert.Null(BridgeLocator.Find(512,96,(_,_)=>((byte)17,(byte)39,(byte)24)));
    }
}
