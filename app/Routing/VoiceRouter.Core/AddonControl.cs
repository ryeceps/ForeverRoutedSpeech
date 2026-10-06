using System.Globalization;
using System.Text;

namespace VoiceRouter.Core;

/// <summary>Metadata travels through bound function keys; the clipboard holds only speech.</summary>
public static class AddonControl
{
    public readonly record struct Stroke(ushort Key,bool Released);
    public const ushort BeginKey=0x80, CommitKey=0x81, CancelKey=0x82; // F17/F18/F19
    public static string Encode(string text,string hint,string nonce)
    {
        _=AddonEnvelope.Encode(text,hint,false,nonce); // shared validation
        int length=Encoding.UTF8.GetByteCount(text);
        string header=string.Create(CultureInfo.InvariantCulture,$"frs3 {nonce} {hint} {length}");
        uint hash=StatusProtocol.Checksum(Encoding.UTF8.GetBytes(header+" "+text));
        return Convert.ToHexString(Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture,$"{header} {hash:x8}"))).ToLowerInvariant();
    }
    public static ushort[] Keys(string text,string hint,string nonce)
    {
        string hex=Encode(text,hint,nonce);
        var keys=new List<ushort>{BeginKey};
        foreach(char c in hex)
        {
            int nibble=c<='9' ? c-'0' : c-'a'+10;
            // Two base-4 digits per nibble: only F13-F16, never ordinary/system F1-F12.
            keys.Add((ushort)(0x7c+nibble/4));
            keys.Add((ushort)(0x7c+nibble%4));
        }
        keys.Add(CommitKey);
        return [..keys];
    }

    /// <summary>The actual Windows stroke plan. No Alt, Win, ordinary function keys or submit keys.</summary>
    public static Stroke[] Strokes(IReadOnlyList<ushort> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if(keys.Count==0 || keys.Any(key=>key<0x7c || key>CancelKey))
            throw new ArgumentException("Routing control permits F13-F19 only.",nameof(keys));
        var strokes=new List<Stroke>{new(0x11,false),new(0x10,false)};
        foreach(ushort key in keys) {strokes.Add(new(key,false));strokes.Add(new(key,true));}
        strokes.Add(new(0x10,true));strokes.Add(new(0x11,true));
        return [..strokes];
    }
}
