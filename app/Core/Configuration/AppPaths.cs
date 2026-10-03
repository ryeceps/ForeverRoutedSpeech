namespace SpeakForever.Configuration;

/// <summary>Where the app and the CLI keep their shared state.</summary>
public static class AppPaths
{
    static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    /// <summary>%LOCALAPPDATA%\SpeakForever, or the SPEAKFOREVER_DATA folder if that's set (tests use it to stay off real data).</summary>
    public static string Root { get; } = Environment.GetEnvironmentVariable("SPEAKFOREVER_DATA") is { Length: > 0 } custom
        ? Path.GetFullPath(custom)
        : Path.Combine(LocalAppData, "ForeverRoutedSpeech");
    public static string Config { get; } = Path.Combine(Root, "foreverroutedspeech.json");
    public static string Models { get; } = Path.Combine(Root, "models");
    public static string Benchmark { get; } = Path.Combine(Root, "benchmark");
    /// <summary>Where a downloaded installer waits to be run; cleared at the next launch.</summary>
    public static string Updates { get; } = Path.Combine(Root, "updates");

    public static IReadOnlyList<string> OldRoots { get; } = [];
    /// <summary>This independent fork does not migrate upstream data.</summary>
    public static void MigrateFromOldName() { }
}
