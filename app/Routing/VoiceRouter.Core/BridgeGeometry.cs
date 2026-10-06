namespace VoiceRouter.Core;

public static class BridgeGeometry
{
    // Keep aligned with the addon's physical bottom inset.
    public const int LeftInset = 16, BottomInset = 32;
    public const int SearchHeight = 96;
    public static int CaptureTop(int visibleBottom) => visibleBottom - BottomInset - StatusProtocol.Rows;
}
