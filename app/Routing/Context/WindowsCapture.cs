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
    public static GameContext Read(Settings settings)
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
        if(!GetClientRect(window,out var bounds)) throw new IOException("Cannot read game client bounds.");
        if(settings.CellPixels < 2 || settings.CellPixels > 16 || !double.IsFinite(settings.CellPixels)) throw new IOException("Calibrate cell pitch between 2 and 16 physical pixels.");
        int width = (int)Math.Ceiling(StatusProtocol.Columns*settings.CellPixels), height = (int)Math.Ceiling(StatusProtocol.Rows*settings.CellPixels);
        if(settings.StripX < 0 || settings.StripY < 0 || settings.StripX+width > bounds.Right || settings.StripY+height > bounds.Bottom) throw new IOException("Status strip lies outside the game client.");
        var origin = new Point { X=settings.StripX,Y=settings.StripY };
        if(!ClientToScreen(window,ref origin)) throw new IOException("Cannot locate game window.");
        using var bitmap = new Bitmap(width,height,System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using(var graphics=Graphics.FromImage(bitmap)) graphics.CopyFromScreen(origin.X,origin.Y,0,0,bitmap.Size,CopyPixelOperation.SourceCopy);
        return PixelStrip.Decode(settings.CellPixels,(x,y)=>{var color=bitmap.GetPixel(x,y);return(color.R,color.G,color.B);});
    }
}
