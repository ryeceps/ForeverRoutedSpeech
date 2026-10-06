using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using VoiceRouter.Core;

namespace VoiceRouter.App;
public static class WindowsCapture
{
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X,Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left,Top,Right,Bottom; }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    private delegate bool WindowVisitor(nint window,nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowVisitor visitor,nint parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetWindowText(nint h,System.Text.StringBuilder text,int count);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint h);
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint h,out Rect rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(nint h,ref Point point);

    private static string Title(nint window)
    {var title=new System.Text.StringBuilder(512);GetWindowText(window,title,title.Capacity);return title.ToString();}
    public static bool IsGameForeground(Settings settings)=>Title(GetForegroundWindow()).Equals(settings.WindowTitle,StringComparison.OrdinalIgnoreCase);
    private static readonly object discoveryGate = new();
    private static Task? discovery;
    private static long nextDiscovery;
    /// <summary>Read the small saved region; rediscover asynchronously if it is missing or moved.</summary>
    public static GameContext ReadAuto(Settings settings)
    {
        try { return Read(settings); }
        catch (Exception error) when (error is IOException or FormatException or ExternalException or ArgumentException)
        {
            lock (discoveryGate)
            {
                if ((discovery is null || discovery.IsCompleted) && Environment.TickCount64 >= nextDiscovery)
                {
                    nextDiscovery = Environment.TickCount64 + 30000;
                    discovery = Task.Run(() =>
                    {
                        try { Calibrate(settings); }
                        catch (Exception failure) when (failure is IOException or FormatException or ExternalException or ArgumentException or InvalidOperationException or UnauthorizedAccessException) { }
                    });
                }
            }
            throw; // No copying/pasting based on a guessed capture region.
        }
    }
    private static nint FindGameWindow(Settings settings)
    {
        nint window=GetForegroundWindow();
        if(!Title(window).Equals(settings.WindowTitle,StringComparison.OrdinalIgnoreCase))
        {
            var matches=new List<nint>();
            EnumWindows((candidate,_)=>{if(IsWindowVisible(candidate) && Title(candidate).Equals(settings.WindowTitle,StringComparison.OrdinalIgnoreCase)) matches.Add(candidate);return true;},0);
            if(matches.Count!=1) throw new IOException("Game window is missing or ambiguous.");
            window=matches[0];
        }
        // An unobscured background strip is usable for editing; autosend separately requires foreground.
        if(window==0 || IsIconic(window) || !IsWindowVisible(window)) throw new IOException("Game is minimized or hidden.");
        return window;
    }
    /// <summary>One-time setup only: find a unique checksummed strip inside the game client.</summary>
    public static Settings Calibrate(Settings settings)
    {
        nint window=FindGameWindow(settings);
        if(!GetClientRect(window,out var bounds)) throw new IOException("Cannot read game client bounds.");
        var origin=new Point();
        if(!ClientToScreen(window,ref origin)) throw new IOException("Cannot locate game window.");
        using var bitmap=new Bitmap(bounds.Right,bounds.Bottom,System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using(var graphics=Graphics.FromImage(bitmap)) graphics.CopyFromScreen(origin.X,origin.Y,0,0,bitmap.Size,CopyPixelOperation.SourceCopy);
        var area=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),System.Drawing.Imaging.ImageLockMode.ReadOnly,System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        byte[] rgb=new byte[area.Stride*area.Height];
        try {Marshal.Copy(area.Scan0,rgb,0,rgb.Length);} finally {bitmap.UnlockBits(area);}
        int stride=area.Stride;
        (byte R,byte G,byte B) Pixel(int x,int y) {int i=y*stride+x*3;return(rgb[i+2],rgb[i+1],rgb[i]);}
        byte[] magic="WVR1"u8.ToArray();
        var matches=new List<(Settings Settings,GameContext Context)>();
        foreach(int pitch in new[]{1,2,3,4})
        for(int y=0;y<=bitmap.Height-StatusProtocol.Rows*pitch;y++)
        for(int x=0;x<=bitmap.Width-StatusProtocol.Columns*pitch;x++)
        {
            bool match=true;
            for(int bit=0;bit<32;bit++)
            {
                var c=Pixel(x+(int)((bit+.5)*pitch),y+(int)(.5*pitch));
                bool white=((magic[bit/8]>>(bit%8))&1)!=0;
                if(Math.Abs(c.R-c.G)>20 || Math.Abs(c.G-c.B)>20 || (white ? c.R<190 : c.R>65)) {match=false;break;}
            }
            if(!match) continue;
            try
            {
                var context=PixelStrip.Decode(pitch,(dx,dy)=>Pixel(x+dx,y+dy));
                matches.Add((settings with {StripX=x,StripY=y,CellPixels=pitch},context));
            }
            catch(FormatException) { }
        }
        // Larger cells can produce several adjacent sampling alignments. Group those as one strip.
        var distinct=new List<(Settings Settings,GameContext Context)>();
        foreach(var candidate in matches)
            if(!distinct.Any(m=>m.Context.Session==candidate.Context.Session && Math.Abs(m.Settings.StripX-candidate.Settings.StripX)<candidate.Settings.CellPixels && Math.Abs(m.Settings.StripY-candidate.Settings.StripY)<candidate.Settings.CellPixels)) distinct.Add(candidate);
        if(distinct.Count!=1) throw new IOException($"Found {distinct.Count} status strips. Keep one strip visible and unobscured, then retry calibration.");
        var found=distinct[0];
        Thread.Sleep(350);
        var next=Read(found.Settings);
        if(next.Session!=found.Context.Session || next.Heartbeat==found.Context.Heartbeat) throw new IOException("Strip heartbeat is not advancing. Calibration was not saved.");
        found.Settings.Save();
        return found.Settings;
    }
    public static GameContext Read(Settings settings)
    {
        nint window=FindGameWindow(settings);
        if(!GetClientRect(window,out var bounds)) throw new IOException("Cannot read game client bounds.");
        if(settings.CellPixels < 1 || settings.CellPixels > 16 || !double.IsFinite(settings.CellPixels)) throw new IOException("Calibrate cell pitch between 1 and 16 physical pixels.");
        int width = (int)Math.Ceiling(StatusProtocol.Columns*settings.CellPixels), height = (int)Math.Ceiling(StatusProtocol.Rows*settings.CellPixels);
        if(settings.StripX < 0 || settings.StripY < 0 || settings.StripX+width > bounds.Right || settings.StripY+height > bounds.Bottom) throw new IOException("Status strip lies outside the game client.");
        var origin = new Point { X=settings.StripX,Y=settings.StripY };
        if(!ClientToScreen(window,ref origin)) throw new IOException("Cannot locate game window.");
        using var bitmap = new Bitmap(width,height,System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using(var graphics=Graphics.FromImage(bitmap)) graphics.CopyFromScreen(origin.X,origin.Y,0,0,bitmap.Size,CopyPixelOperation.SourceCopy);
        try {return PixelStrip.Decode(settings.CellPixels,(x,y)=>{var color=bitmap.GetPixel(x,y);return(color.R,color.G,color.B);});}
        catch(FormatException e) {throw new FormatException($"{e.Message} Capture ({settings.StripX},{settings.StripY}), pitch {settings.CellPixels}; game client {bounds.Right} x {bounds.Bottom}.",e);}
    }
}
