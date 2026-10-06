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
    [DllImport("user32.dll")] private static extern nint SetThreadDpiAwarenessContext(nint context);
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    private sealed class PhysicalPixels : IDisposable
    {
        private readonly nint previous = SetThreadDpiAwarenessContext((nint)(-4));
        public void Dispose() { if (previous != 0) SetThreadDpiAwarenessContext(previous); }
    }

    private static string Title(nint window)
    {var title=new System.Text.StringBuilder(512);GetWindowText(window,title,title.Capacity);return title.ToString();}
    public static bool IsGameForeground(Settings settings)=>Title(GetForegroundWindow()).Equals(settings.WindowTitle,StringComparison.OrdinalIgnoreCase);
    private static readonly object discoveryGate = new();
    private static Task? discovery;
    private static long nextDiscovery;
    private sealed record EdgeCapture(Settings Settings, int Width, int Height, int Left, int Bottom);
    private static volatile EdgeCapture? edgeCapture;
    /// <summary>Read the small saved region; rediscover asynchronously if it is missing or moved.</summary>
    public static GameContext ReadAuto(Settings settings)
    {
        string? edgeError = null;
        try
        {
            using var dpi = new PhysicalPixels();
            var window = FindGameWindow(settings);
            if (GetClientRect(window, out var bounds))
            {
                var origin = new Point();
                if (!ClientToScreen(window, ref origin)) throw new IOException("Cannot locate game window.");
                var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
                if (!GetMonitorInfo(MonitorFromWindow(window, 2), ref monitor)) throw new IOException("Cannot locate game display.");
                // Borderless/maximized windows can report client pixels beyond the display.
                // The rendered UI edge is at the visible monitor boundary, not those off-screen pixels.
                var edge = settings with { StripX = Math.Max(origin.X, monitor.Monitor.Left) - origin.X,
                    StripY = Math.Min(origin.Y + bounds.Bottom, monitor.Monitor.Bottom) - origin.Y - StatusProtocol.Rows*2,
                    CellPixels = 2, StripColumns = StatusProtocol.Columns };
                var cached = edgeCapture;
                if(cached is not null && cached.Width == bounds.Right && cached.Height == bounds.Bottom && cached.Left == edge.StripX && cached.Bottom == edge.StripY)
                {
                    try { return Read(cached.Settings); }
                    catch(Exception stale) when(stale is IOException or FormatException or ExternalException or ArgumentException) { edgeCapture=null; }
                }
                try
                {
                    var context = Read(edge);
                    edgeCapture = new(edge,bounds.Right,bounds.Bottom,edge.StripX,edge.StripY);
                    return context;
                }
                catch (Exception missing) when (missing is IOException or FormatException or ExternalException or ArgumentException) { edgeError = missing.Message; }
                if(IsGameForeground(settings))
                {
                    var found = FindEdge(edge, bounds, origin, monitor.Monitor);
                    if(found is { } bridge)
                    {
                        edgeCapture = new(bridge.Settings,bounds.Right,bounds.Bottom,edge.StripX,edge.StripY);
                        return bridge.Context;
                    }
                }
            }
            return Read(settings);
        }
        catch (Exception error) when (error is IOException or FormatException or ExternalException or ArgumentException)
        {
            lock (discoveryGate)
            {
                if (IsGameForeground(settings) && (discovery is null || discovery.IsCompleted) && Environment.TickCount64 >= nextDiscovery)
                {
                    nextDiscovery = Environment.TickCount64 + 30000;
                    discovery = Task.Run(() =>
                    {
                        try { Calibrate(settings); }
                        catch (Exception failure) when (failure is IOException or FormatException or ExternalException or ArgumentException or InvalidOperationException or UnauthorizedAccessException) { }
                    });
                }
            }
            throw new IOException("Automatic bridge: " + edgeError + " Saved capture: " + error.Message, error);
        }
    }
    private static (Settings Settings, GameContext Context)? FindEdge(Settings settings, Rect bounds, Point origin, Rect monitor)
    {
        int left=Math.Max(origin.X,monitor.Left), right=Math.Min(origin.X+bounds.Right,monitor.Right);
        int bottom=Math.Min(origin.Y+bounds.Bottom,monitor.Bottom), top=Math.Max(origin.Y,bottom-64);
        int width=right-left,height=bottom-top;
        if(width<StatusProtocol.Columns || height<StatusProtocol.Rows) return null;
        using var bitmap=new Bitmap(width,height,System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using(var graphics=Graphics.FromImage(bitmap)) graphics.CopyFromScreen(left,top,0,0,bitmap.Size,CopyPixelOperation.SourceCopy);
        var area=bitmap.LockBits(new Rectangle(0,0,width,height),System.Drawing.Imaging.ImageLockMode.ReadOnly,System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        byte[] rgb=new byte[area.Stride*height]; int stride=area.Stride;
        try { Marshal.Copy(area.Scan0,rgb,0,rgb.Length); } finally { bitmap.UnlockBits(area); }
        (byte R,byte G,byte B) Pixel(int x,int y) { int i=y*stride+x*3;return(rgb[i+2],rgb[i+1],rgb[i]); }
        byte[] magic="WVR1"u8.ToArray();
        foreach(double pitch in new[]{1d,1.25,1.5,2,2.5,3,4})
        for(int y=0;y<=height-Math.Ceiling(StatusProtocol.Rows*pitch);y++)
        for(int x=0;x<=width-Math.Ceiling(StatusProtocol.Columns*pitch);x++)
        {
            bool match=true;
            var first=Pixel(x+(int)(.5*pitch),y+(int)(.5*pitch)); bool dim=first.R<190;
            for(int bit=0;bit<32;bit++)
            {
                var c=Pixel(x+(int)((bit+.5)*pitch),y+(int)(.5*pitch));
                bool one=((magic[bit/8]>>(bit%8))&1)!=0;
                if(Math.Abs(c.R-c.G)>20 || Math.Abs(c.G-c.B)>20 || (one ? c.R<(dim?28:190) || dim && c.R>160 : c.R>(dim?18:65))) {match=false;break;}
            }
            if(!match) continue;
            try
            {
                var context=PixelStrip.Decode(pitch,(dx,dy)=>Pixel(x+dx,y+dy));
                return (settings with {StripX=left-origin.X+x,StripY=top-origin.Y+y,CellPixels=pitch},context);
            }
            catch(FormatException) { }
        }
        return null;
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
        using var dpi = new PhysicalPixels();
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
        foreach(int columns in new[]{StatusProtocol.Columns,128})
        foreach(int pitch in new[]{1,2,3,4})
        for(int y=0;y<=bitmap.Height-(StatusProtocol.Capacity*8/columns)*pitch;y++)
        for(int x=0;x<=bitmap.Width-columns*pitch;x++)
        {
            bool match=true;
            bool dim=Pixel(x+(int)(.5*pitch),y+(int)(.5*pitch)).R<190;
            for(int bit=0;bit<32;bit++)
            {
                var c=Pixel(x+(int)((bit+.5)*pitch),y+(int)(.5*pitch));
                bool white=((magic[bit/8]>>(bit%8))&1)!=0;
                if(Math.Abs(c.R-c.G)>20 || Math.Abs(c.G-c.B)>20 || (white ? c.R<(dim ? 28 : 190) || dim && c.R>160 : c.R>(dim ? 18 : 65))) {match=false;break;}
            }
            if(!match) continue;
            try
            {
                var context=PixelStrip.Decode(pitch,(dx,dy)=>Pixel(x+dx,y+dy),columns);
                matches.Add((settings with {StripX=x,StripY=y,CellPixels=pitch,StripColumns=columns},context));
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
        using var dpi = new PhysicalPixels();
        nint window=FindGameWindow(settings);
        if(!GetClientRect(window,out var bounds)) throw new IOException("Cannot read game client bounds.");
        if(settings.CellPixels < 1 || settings.CellPixels > 16 || !double.IsFinite(settings.CellPixels)) throw new IOException("Calibrate cell pitch between 1 and 16 physical pixels.");
        if (settings.StripColumns is not (128 or 512)) throw new IOException("Unsupported bridge layout.");
        int width = (int)Math.Ceiling(settings.StripColumns*settings.CellPixels), height = (int)Math.Ceiling(StatusProtocol.Capacity*8/settings.StripColumns*settings.CellPixels);
        if(settings.StripX < 0 || settings.StripY < 0 || settings.StripX+width > bounds.Right || settings.StripY+height > bounds.Bottom) throw new IOException("Status strip lies outside the game client.");
        var origin = new Point { X=settings.StripX,Y=settings.StripY };
        if(!ClientToScreen(window,ref origin)) throw new IOException("Cannot locate game window.");
        using var bitmap = new Bitmap(width,height,System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using(var graphics=Graphics.FromImage(bitmap)) graphics.CopyFromScreen(origin.X,origin.Y,0,0,bitmap.Size,CopyPixelOperation.SourceCopy);
        try {return PixelStrip.Decode(settings.CellPixels,(x,y)=>{var color=bitmap.GetPixel(x,y);return(color.R,color.G,color.B);},settings.StripColumns);}
        catch(FormatException e) {throw new FormatException($"{e.Message} Capture ({settings.StripX},{settings.StripY}), pitch {settings.CellPixels}; game client {bounds.Right} x {bounds.Bottom}.",e);}
    }
}
