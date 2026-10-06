using System.Globalization;
using System.Text;

namespace VoiceRouter.Core;

/// <summary>Metadata travels through bound function keys; the clipboard holds only speech.</summary>
public static class AddonControl
{
    public const ushort BeginKey=0x80, CommitKey=0x81, CancelKey=0x82; // F17/F18/F19
    public static string Encode(string text,string hint,string nonce)
    {
        _=AddonEnvelope.Encode(text,hint,false,nonce); // shared validation
        int length=Encoding.UTF8.GetByteCount(text);
        string header=string.Create(CultureInfo.InvariantCulture,$"frs2 {nonce} {hint} {length}");
        uint hash=StatusProtocol.Checksum(Encoding.UTF8.GetBytes(header+" "+text));
        return Convert.ToHexString(Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture,$"{header} {hash:x8}"))).ToLowerInvariant();
    }
    public static ushort[] Keys(string text,string hint,string nonce)
    {
        string hex=Encode(text,hint,nonce);
        return [BeginKey,..hex.Select(c=>(ushort)(0x70+(c<='9' ? c-'0' : c-'a'+10))),CommitKey];
    }
}
