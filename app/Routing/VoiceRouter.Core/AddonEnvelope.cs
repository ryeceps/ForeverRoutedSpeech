using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace VoiceRouter.Core;

/// <summary>One-way draft transport. No game context or executable Lua is transported.</summary>
public static class AddonEnvelope
{
    public const int MessageByteLimit = 200;
    public const string Prefix = "/frs1 ";
    public static string? ValidateMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "No speech transcript; clipboard retained.";
        if (text.TrimStart().StartsWith('/')) return "Leading chat commands are not allowed in a spoken draft.";
        if (text.Any(char.IsControl)) return "Remove line breaks and control characters before pasting.";
        if (Encoding.UTF8.GetByteCount(text)>MessageByteLimit) return "Draft exceeds 200 UTF-8 bytes. Edit it; nothing was truncated.";
        return null;
    }
    public static bool ValidHint(string hint) => Regex.IsMatch(hint,
        @"^(?:default|[im]:(?:say|guild|party|raid|instance|general|trade|lookingforgroup)|m:channel:[1-9][0-9]{0,3})$",
        RegexOptions.CultureInvariant);
    public static string Encode(string text, string hint, bool send, string nonce)
    {
        if (ValidateMessage(text) is { } error) throw new ArgumentException(error,nameof(text));
        if (!ValidHint(hint) || !Regex.IsMatch(nonce,@"^[a-f0-9]{32}$",RegexOptions.CultureInvariant))
            throw new ArgumentException("Invalid addon envelope metadata.");
        var bytes=Encoding.UTF8.GetBytes(text);
        string protectedText=string.Create(CultureInfo.InvariantCulture,$"{Prefix}{nonce} {hint} {(send ? 1 : 0)} {bytes.Length} {text}");
        uint checksum=StatusProtocol.Checksum(Encoding.UTF8.GetBytes(protectedText));
        return string.Create(CultureInfo.InvariantCulture,$"{Prefix}{nonce} {hint} {(send ? 1 : 0)} {bytes.Length} {checksum:x8} {text}");
    }
}
