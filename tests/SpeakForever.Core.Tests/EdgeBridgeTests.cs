using System.Buffers.Binary;
using System.Text;
using VoiceRouter.Core;

namespace SpeakForever.Core.Tests;

public class EdgeBridgeTests
{
    static byte[] Frame(string version = "5")
    {
        string payload = version + "\ttest\tparty\t1\t0\tbytes\t\t1,General,General\tclosed\tnone\t\t0\tchars\tnone\t\t-1\t";
        if (version == "5") payload += "\tIronforge\tThe%20Commons\t1\t1";
        var text = Encoding.UTF8.GetBytes(payload);
        var result = new byte[StatusProtocol.Capacity];
        "WVR1"u8.CopyTo(result);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(4), (ushort)text.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(6), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(10), 2);
        text.CopyTo(result.AsSpan(14));
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(14 + text.Length), StatusProtocol.Checksum(result.AsSpan(0,14 + text.Length)));
        return result;
    }

    [Theory]
    [InlineData(512, 64)]
    [InlineData(128, 255)]
    public void NewDimEdgeAndOldBrightLayoutBothDecode(int columns, byte white)
    {
        var bytes = Frame();
        var context = PixelStrip.Decode(1, (x,y) =>
        {
            int bit = y * columns + x;
            byte value = ((bytes[bit / 8] >> (bit % 8)) & 1) == 1 ? white : (byte)0;
            return (value,value,value);
        }, columns);
        Assert.Equal(GroupCategory.Party, context.Group);
        Assert.Equal("Ironforge", context.Zone);
        Assert.Equal("The Commons", context.Subzone);
        Assert.True(context.InCity);
        Assert.True(context.Resting);
    }

    [Fact]
    public void OlderAddonStillDecodesWithoutInventingLocation()
    {
        var context = StatusProtocol.Decode(Frame("4"));
        Assert.Equal(4, context.ProtocolVersion);
        Assert.Null(context.InCity);
        Assert.Null(context.Zone);
    }

    [Fact]
    public void InvalidDimSignalCannotBecomeFreshContext()
    {
        Assert.Throws<FormatException>(() => PixelStrip.Decode(1, (_,_) => ((byte)24,(byte)24,(byte)24)));
        var frame = Frame(); frame[24] ^= 1;
        Assert.Throws<FormatException>(() => StatusProtocol.Decode(frame));
    }
}
