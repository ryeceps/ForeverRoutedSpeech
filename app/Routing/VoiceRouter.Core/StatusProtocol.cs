using System.Buffers.Binary;
using System.Text;

namespace VoiceRouter.Core;

public static class StatusProtocol
{
    public const int Columns = 128, Rows = 32, Capacity = Columns * Rows / 8;
    public static uint Checksum(ReadOnlySpan<byte> bytes)
    {
        uint a = 1, b = 0;
        foreach (byte v in bytes) { a = (a + v) % 65521; b = (b + a) % 65521; }
        return (b << 16) | a;
    }
    public static GameContext Decode(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 18 || !frame[..4].SequenceEqual("WVR1"u8)) throw new FormatException("Invalid framing.");
        int length = BinaryPrimitives.ReadUInt16LittleEndian(frame[4..]);
        if (length > Capacity - 18 || length < 1 || frame.Length < length + 18) throw new FormatException("Invalid payload size.");
        if (Checksum(frame[..(14 + length)]) != BinaryPrimitives.ReadUInt32LittleEndian(frame[(14 + length)..])) throw new FormatException("Checksum failed.");
        uint session = BinaryPrimitives.ReadUInt32LittleEndian(frame[6..]), sequence = BinaryPrimitives.ReadUInt32LittleEndian(frame[10..]);
        var fields = new UTF8Encoding(false, true).GetString(frame.Slice(14, length)).Split('\t');
        bool location=fields.Length==21 && fields[0]=="5";
        bool echo=location || fields.Length==17 && fields[0]=="4";
        bool activePanel=echo || fields.Length==15 && fields[0]=="3";
        bool extended=activePanel || fields.Length==13 && fields[0]=="2";
        if ((!extended && (fields.Length!=9 || fields[0]!="1")) || fields[1].Length==0) throw new FormatException("Invalid context fields.");
        var group = fields[2] switch { "solo" => GroupCategory.Solo, "party" => GroupCategory.Party, "raid" => GroupCategory.Raid, "instance" => GroupCategory.Instance, _ => throw new FormatException("Invalid group.") };
        if (fields[3] != "0" && fields[3] != "1") throw new FormatException("Invalid guild.");
        if (!int.TryParse(fields[4], out int limit) || limit < 0 || limit > 4096 || fields[5] is not ("bytes" or "chars")) throw new FormatException("Invalid limit.");
        var prefixes = new Dictionary<Destination, string>();
        if (fields[6].Length > 0)
            foreach (var p in fields[6].Split(';'))
            {
                var pair = p.Split('=');
                if (pair.Length != 2 || !Enum.TryParse<Destination>(pair[0], true, out var kind) || !Enum.IsDefined(kind) || !prefixes.TryAdd(kind, pair[1])) throw new FormatException("Invalid prefix.");
                var allowed = kind switch { Destination.Say => new[]{"/say","/s"}, Destination.Guild => new[]{"/guild","/g"}, Destination.Party => new[]{"/party","/p"}, Destination.Raid => new[]{"/raid","/ra"}, Destination.Instance => new[]{"/instance","/i"}, Destination.Custom => new[]{"numbered"}, _ => [] };
                if (!allowed.Contains(pair[1])) throw new FormatException("Unrecognized prefix.");
            }
        var channels = new List<Channel>();
        if (fields[7].Length > 0)
            foreach (var channel in fields[7].Split(';'))
            {
                var values = channel.Split(',');
                if (values.Length != 3 || !int.TryParse(values[0], out int id) || id < 1 || id > 999 || channels.Any(c => c.Id == id) ||
                    !Enum.TryParse<Destination>(values[1], true, out var kind) || kind is not (Destination.General or Destination.Trade or Destination.LookingForGroup or Destination.Custom)) throw new FormatException("Invalid channel.");
                string name = Uri.UnescapeDataString(values[2]);
                if (string.IsNullOrWhiteSpace(name) || name.Length > 128) throw new FormatException("Invalid channel name.");
                channels.Add(new(id, name, kind));
            }
        var chatInput = fields[8] switch { "closed" => ChatInputState.Closed, "open" => ChatInputState.Open, "unknown" => ChatInputState.Unknown, _ => throw new FormatException("Invalid chat input state.") };
        TextTarget? textTarget=null;
        if(extended)
        {
            if(fields[9] is not ("none" or "auctionhouse" or "search" or "unsupported") || !int.TryParse(fields[11],out int fieldLimit) || fieldLimit<0 || fieldLimit>4096 || fields[12] is not ("bytes" or "chars")) throw new FormatException("Invalid focused text field.");
            if(fields[9]!="none")
            {
                string name=Uri.UnescapeDataString(fields[10]);
                if(string.IsNullOrWhiteSpace(name) || name.Length>128) throw new FormatException("Invalid field identity.");
                textTarget=new(name,fields[9]=="auctionhouse" ? TextFieldKind.AuctionHouse : fields[9]=="search" ? TextFieldKind.Search : TextFieldKind.Unsupported,fieldLimit,fields[12]=="bytes");
            }
        }
        Destination? activeDestination=null;int? activeChannelId=null;bool unsupported=false;
        if(activePanel)
        {
            if(fields[13]=="unsupported") unsupported=true;
            else if(fields[13]!="none")
            {
                if(!Enum.TryParse<Destination>(fields[13],true,out var destination) || !Enum.IsDefined(destination) || destination==Destination.Default)
                    throw new FormatException("Invalid active chat destination.");
                activeDestination=destination;
            }
            if(fields[14].Length>0)
            {
                if(!int.TryParse(fields[14],out int id) || id<1 || id>999 || activeDestination is not (Destination.General or Destination.Trade or Destination.LookingForGroup or Destination.Custom))
                    throw new FormatException("Invalid active channel ID.");
                activeChannelId=id;
            }
            if(activeDestination is Destination.General or Destination.Trade or Destination.LookingForGroup or Destination.Custom && activeChannelId is null)
                throw new FormatException("Missing active channel ID.");
        }
        int? inputBytes=null;uint? inputChecksum=null;
        if(echo && fields[15]!="-1")
        {
            if(!int.TryParse(fields[15],out int count) || count<0 || count>65536 || !uint.TryParse(fields[16],out uint hash)) throw new FormatException("Invalid field echo.");
            inputBytes=count;inputChecksum=hash;
        }
        else if(echo && fields[16]!="") throw new FormatException("Invalid absent field echo.");
        bool? Flag(string value) => value switch { "1" => true, "0" => false, "?" => null, _ => throw new FormatException("Invalid location flag.") };
        return new(location ? 5 : echo ? 4 : activePanel ? 3 : extended ? 2 : 1, fields[1], session, sequence, group, fields[3] == "1", channels, prefixes, limit, fields[5] == "bytes", chatInput,textTarget,activeDestination,activeChannelId,unsupported,inputBytes,inputChecksum,
            Zone: location ? Uri.UnescapeDataString(fields[17]) : null, Subzone: location ? Uri.UnescapeDataString(fields[18]) : null,
            InCity: location ? Flag(fields[19]) : null, Resting: location ? Flag(fields[20]) : null);
    }
}

public sealed class ContextTracker
{
    public GameContext? Current { get; private set; }
    private TimeSpan lastAdvance;
    private bool valid;
    public void Accept(GameContext context, TimeSpan monotonicTime)
    {
        if (Current is null || Current.Session != context.Session || unchecked((int)(context.Heartbeat - Current.Heartbeat)) > 0)
        { Current = context; lastAdvance = monotonicTime; valid = true; }
        // Replayed frames cannot extend freshness, even with a valid checksum.
    }
    public void Invalidate() => valid = false;
    public bool IsFresh(TimeSpan now) => valid && Current is not null && now >= lastAdvance && now - lastAdvance < TimeSpan.FromSeconds(2);
}
