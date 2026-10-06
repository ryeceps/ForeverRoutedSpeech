using System.Runtime.InteropServices;
using System.Diagnostics;
using VoiceRouter.App;

namespace SpeakForever.Interop;

/// <summary>
/// Clipboard handling and a single user-requested paste shortcut. Never sends chat.
/// </summary>
public static partial class Native
{
    const uint CF_UNICODETEXT = 13, GMEM_MOVEABLE = 0x2;
    static readonly int[] PasteModifiers = [0x10, 0x11, 0x12, 0x5B, 0x5C, 0x56, 0x0D, 0x1B];
    const int OpenAttempts = 10, OpenRetryMs = 20;
    static readonly IntPtr HWND_MESSAGE = -3;

    // Clipboard formats Windows reads to keep an item out of clipboard history (Win+V) and cloud sync.
    static readonly uint NoHistory = RegisterClipboardFormat("CanIncludeInClipboardHistory");
    static readonly uint NoCloud = RegisterClipboardFormat("CanUploadToCloudClipboard");

    /// <summary>
    /// Puts text on the clipboard, kept out of clipboard history and cloud sync: it's a chat message
    /// waiting to be pasted, not something to keep. Returns null on success or an error description;
    /// <paramref name="version"/> is the clipboard's sequence number after, to tell later whether it's still ours.
    /// </summary>
    public static bool OwnsClipboard(uint version) => version != 0 && GetClipboardSequenceNumber() == version;
    public static string? CopyText(string text, out uint version, uint? expectedVersion = null)
    {
        version = 0;
        // SetClipboardData needs an owner window; a message-only one is enough, and the text outlives it.
        var owner = CreateWindowEx(0, "STATIC", "", 0, 0, 0, 0, 0, HWND_MESSAGE, 0, 0, 0);
        try
        {
            if (!OpenClipboardPatiently(owner)) return "Couldn't copy it: another program is using the clipboard. Try again.";
            try
            {
                if(expectedVersion is uint expected && !OwnsClipboard(expected))
                    return "Clipboard changed. Draft retained; nothing was overwritten.";
                EmptyClipboard();
                Span<byte> no = stackalloc byte[sizeof(int)]; // a DWORD 0
                no.Clear();
                if (!SetData(CF_UNICODETEXT, MemoryMarshal.AsBytes((text + "\0").AsSpan())))
                    return $"Couldn't copy it: Windows refused the clipboard (error {Marshal.GetLastPInvokeError()}).";
                SetData(NoHistory, no);
                SetData(NoCloud, no);
            }
            finally
            {
                CloseClipboard();
            }
            version = GetClipboardSequenceNumber();
            return null;
        }
        finally
        {
            if (owner != 0) DestroyWindow(owner);
        }
    }

    /// <summary>Empties the clipboard if it still holds what was copied at <paramref name="version"/>, and not something copied since.</summary>
    public static void ClearClipboard(uint version)
    {
        if (GetClipboardSequenceNumber() != version || !OpenClipboardPatiently(0)) return;
        EmptyClipboard();
        CloseClipboard();
    }

    // INPUT has a 32-byte union on Windows x64. Keyboard data begins at offset 8.
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    struct PasteInput
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public ushort Key;
        [FieldOffset(12)] public uint Flags;
    }

    /// <summary>Requests Ctrl+V once on Windows x64, only for our unchanged clipboard and focused WoW.</summary>
    public static string? PasteCopied(uint version, out bool attempted)
    {
        attempted = false;
        if (CheckInput(version) is { } blocked) return blocked;
        PasteInput[] keys = [new() { Type = 1, Key = 0x11 }, new() { Type = 1, Key = 0x56 },
            new() { Type = 1, Key = 0x56, Flags = 2 }, new() { Type = 1, Key = 0x11, Flags = 2 }];
        attempted = true;
        uint sent = SendInput((uint)keys.Length, keys, Marshal.SizeOf<PasteInput>());
        if (sent == keys.Length) return null;
        // Release only; never replay a partial paste or elevate to bypass Windows input restrictions.
        PasteInput[] release = [new() { Type = 1, Key = 0x56, Flags = 2 }, new() { Type = 1, Key = 0x11, Flags = 2 }];
        SendInput((uint)release.Length, release, Marshal.SizeOf<PasteInput>());
        return "Windows did not accept the full paste shortcut. Inspect the game field and paste manually if needed.";
    }

    static string? CheckInput(uint version)
    {
        if (!Environment.Is64BitProcess) return "Controller paste requires Windows x64.";
        nint window = GetForegroundWindow();
        if (!WindowsCapture.IsGameForeground(Settings.Load())) return "Return to the game before pasting.";
        GetWindowThreadProcessId(window, out uint processId);
        try
        {
            using var process = Process.GetProcessById((int)processId);
            if (!process.ProcessName.Equals("Wow", StringComparison.OrdinalIgnoreCase) &&
                !process.ProcessName.Equals("WowB", StringComparison.OrdinalIgnoreCase))
                return "Foreground program is not a recognized WoW client; paste manually.";
        }
        catch (ArgumentException) { return "Game window changed; paste manually."; }
        catch (InvalidOperationException) { return "Cannot identify the game; paste manually."; }
        catch (System.ComponentModel.Win32Exception) { return "Cannot verify the game process; paste manually."; }
        if (PasteModifiers.Any(IsKeyDown))
            return "Release keyboard modifiers before controller paste.";
        if (version == 0 || GetClipboardSequenceNumber() != version)
            return "Clipboard changed. Review and recopy the draft before pasting.";
        if (GetForegroundWindow() != window) return "Game focus changed; paste manually.";
        return null;
    }

    /// <summary>Requests Enter only to open a confirmed closed chat; never to submit text.</summary>
    public static string? OpenChat(uint version, out bool attempted) => RequestEnter(version, out attempted);

    static string? RequestEnter(uint version, out bool attempted)
    {
        attempted = false;
        if (CheckInput(version) is { } blocked) return blocked;
        PasteInput[] keys = [new() { Type = 1, Key = 0x0D }, new() { Type = 1, Key = 0x0D, Flags = 2 }];
        attempted = true;
        if (SendInput(2, keys, Marshal.SizeOf<PasteInput>()) == 2) return null;
        PasteInput[] release = [new() { Type = 1, Key = 0x0D, Flags = 2 }];
        SendInput(1, release, Marshal.SizeOf<PasteInput>());
        return "Windows did not accept the Enter shortcut. Inspect the game; no retry was made.";
    }

    /// <summary>Requests Enter once after the caller confirms pasted text and audience.</summary>
    public static string? SubmitChat(uint version, out bool attempted) => RequestEnter(version, out attempted);

    /// <summary>Requests Escape once after fresh readback confirms an empty chat still has focus.</summary>
    public static string? CloseChat(uint version, out bool attempted)
    {
        attempted = false;
        if (CheckInput(version) is { } blocked) return blocked;
        PasteInput[] keys = [new() { Type = 1, Key = 0x1B }, new() { Type = 1, Key = 0x1B, Flags = 2 }];
        attempted = true;
        if (SendInput(2, keys, Marshal.SizeOf<PasteInput>()) == 2) return null;
        PasteInput[] release = [new() { Type = 1, Key = 0x1B, Flags = 2 }];
        SendInput(1, release, Marshal.SizeOf<PasteInput>());
        return "Windows did not accept the chat-close shortcut. Close chat manually; no retry was made.";
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial uint SendInput(uint count, [In] PasteInput[] inputs, int size);
    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();
    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint window, out uint processId);

    /// <summary>The key is held down right now, in whichever program has focus.</summary>
    public static bool IsKeyDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    /// <summary>Another program may have the clipboard open for a moment; it's only ever briefly.</summary>
    static bool OpenClipboardPatiently(IntPtr owner)
    {
        for (int i = 0; i < OpenAttempts; i++)
        {
            if (OpenClipboard(owner)) return true;
            Thread.Sleep(OpenRetryMs);
        }
        return false;
    }

    static unsafe bool SetData(uint format, ReadOnlySpan<byte> data)
    {
        var memory = GlobalAlloc(GMEM_MOVEABLE, (nuint)data.Length);
        if (memory == 0) return false;
        var at = GlobalLock(memory);
        data.CopyTo(new Span<byte>((void*)at, data.Length));
        GlobalUnlock(memory);
        if (SetClipboardData(format, memory) != 0) return true; // the clipboard owns the memory now
        GlobalFree(memory);
        return false;
    }

    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial IntPtr CreateWindowEx(uint exStyle, string className, string windowName, uint style, int x, int y,
        int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyWindow(IntPtr hWnd);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenClipboard(IntPtr owner);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EmptyClipboard();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseClipboard();

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial IntPtr SetClipboardData(uint format, IntPtr memory);

    [LibraryImport("user32.dll", EntryPoint = "RegisterClipboardFormatW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint RegisterClipboardFormat(string name);

    [LibraryImport("user32.dll")]
    private static partial uint GetClipboardSequenceNumber();

    [LibraryImport("user32.dll")]
    private static partial short GetAsyncKeyState(int virtualKey);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr GlobalAlloc(uint flags, nuint bytes);

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GlobalLock(IntPtr memory);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalUnlock(IntPtr memory);

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GlobalFree(IntPtr memory);
}
