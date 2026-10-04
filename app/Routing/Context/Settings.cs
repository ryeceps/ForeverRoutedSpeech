using System.IO;
using System.Text.Json;

namespace VoiceRouter.App;
public sealed record Settings
{
    public string WindowTitle { get; set; } = "World of Warcraft";
    public int StripX { get; set; } = 16;
    public int StripY { get; set; } = 64;
    public double CellPixels { get; set; } = 1;
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ForeverRoutedSpeech");
    public static string FilePath => Path.Combine(Folder,"capture.json");
    public static Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new(); }
        catch (Exception e) when(e is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }
    public void Save() { Directory.CreateDirectory(Folder); File.WriteAllText(FilePath,JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true})); }
}
