namespace VoiceRouter.Core;
public static class PixelStrip
{
    public static GameContext Decode(double pitch,Func<int,int,(byte R,byte G,byte B)> pixel, int columns = StatusProtocol.Columns)
    {
        if(!double.IsFinite(pitch) || pitch<1 || pitch>16) throw new FormatException("Invalid cell pitch.");
        var bytes=new byte[StatusProtocol.Capacity];
        if (columns is not (128 or 512)) throw new FormatException("Invalid bridge layout.");
        bool dim = pixel((int)(.5*pitch), (int)(.5*pitch)).R < 190;
        for(int bit=0;bit<StatusProtocol.Capacity*8;bit++)
        {
            int x=(int)((bit%columns+.5)*pitch),y=(int)((bit/columns+.5)*pitch);
            var color=pixel(x,y);
            if(Math.Abs(color.R-color.G)>20 || Math.Abs(color.G-color.B)>20 ||
                (dim ? color.R is >18 and <28 or >160 : color.R is >65 and <190)) throw new FormatException($"Bridge capture invalid at bit {bit}: RGB {color.R},{color.G},{color.B}.");
            if(color.R >= (dim ? 28 : 190)) bytes[bit/8]|=(byte)(1<<(bit%8));
        }
        try { return StatusProtocol.Decode(bytes); }
        catch (FormatException error) { throw new FormatException(error.Message + " Header: " + Convert.ToHexString(bytes.AsSpan(0, 8)), error); }
    }
}
