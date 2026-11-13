using System.Text.Json;
using System.Text.Json.Serialization;
using BarudakBogor.Localization;

namespace BarudakBogor.Game;

public enum Quality
{
    Hemat,
    Sedang,
    Tinggi,
}

/// <summary>Player preferences, kept next to the save file.</summary>
public sealed class Settings
{
    public Language Language { get; set; } = Language.Indonesia;

    public Quality Quality { get; set; } = Quality.Sedang;

    public float Volume { get; set; } = 0.8f;

    public bool Muted { get; set; }

    public bool Music { get; set; } = true;

    public bool ShowStats { get; set; }

    /// <summary>Lower the render resolution when the GPU cannot keep up, and raise it again when it can.</summary>
    public bool DynamicResolution { get; set; } = true;

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BarudakBogor", "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize(File.ReadAllText(FilePath), SettingsJsonContext.Default.Settings) ?? new Settings();
            }
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            // A damaged settings file is not worth failing over: fall back to defaults.
        }

        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, SettingsJsonContext.Default.Settings));
        }
        catch (IOException)
        {
            // Preferences are a convenience; the game carries on without them.
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(Settings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
