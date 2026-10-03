namespace VoiceRouter.Core;
public static class PixelStrip
{
    public static GameContext Decode(double pitch,Func<int,int,(byte R,byte G,byte B)> pixel)
    {
        if(!double.IsFinite(pitch) || pitch<2 || pitch>16) throw new FormatException("Invalid cell pitch.");
        var bytes=new byte[StatusProtocol.Capacity];
        for(int bit=0;bit<StatusProtocol.Columns*StatusProtocol.Rows;bit++)
        {
            int x=(int)((bit%StatusProtocol.Columns+.5)*pitch),y=(int)((bit/StatusProtocol.Columns+.5)*pitch);
            var color=pixel(x,y);
            if(Math.Abs(color.R-color.G)>20 || Math.Abs(color.G-color.B)>20 || color.R is >65 and <190) throw new FormatException("Strip obscured or calibration incorrect.");
            if(color.R>=190) bytes[bit/8]|=(byte)(1<<(bit%8));
        }
        return StatusProtocol.Decode(bytes);
    }
}
