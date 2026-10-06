namespace VoiceRouter.Core;

public sealed record BridgeMatch(int X, int Y, double Pitch, int Columns, GameContext Context);

/// <summary>Locate checksummed addon pixels in a captured region. No OS or game input.</summary>
public static class BridgeLocator
{
    public static BridgeMatch? Find(int width, int height, Func<int,int,(byte R,byte G,byte B)> pixel)
    {
        byte[] magic = "WVR1"u8.ToArray();
        foreach (int columns in new[] { StatusProtocol.Columns, 512 })
        foreach (double pitch in new[] { 1d, 1.25, 1.5, 2, 2.5, 3, 4 })
        for (int y=0; y<=height-Math.Ceiling(StatusProtocol.Capacity*8/columns*pitch); y++)
        for (int x=0; x<=width-Math.Ceiling(columns*pitch); x++)
        {
            bool match=true;
            bool dim=pixel(x+(int)(.5*pitch),y+(int)(.5*pitch)).R<190;
            for (int bit=0; bit<32; bit++)
            {
                var c=pixel(x+(int)((bit+.5)*pitch),y+(int)(.5*pitch));
                bool one=((magic[bit/8]>>(bit%8))&1)!=0;
                if (Math.Abs(c.R-c.G)>20 || Math.Abs(c.G-c.B)>20 ||
                    (one ? c.R<(dim?28:190) || dim && c.R>160 : c.R>(dim?18:65)))
                { match=false; break; }
            }
            if (!match) continue;
            try
            {
                return new(x,y,pitch,columns,PixelStrip.Decode(pitch,(dx,dy)=>pixel(x+dx,y+dy),columns));
            }
            catch (FormatException) { }
        }
        return null;
    }
}
