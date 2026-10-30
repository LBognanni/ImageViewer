using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace ImageViewer;

public class SettingsJsonStorage : ISettingsStorage
{
    private readonly string _settingsFile;

    public SettingsJsonStorage() : this(GetDefaultPath())
    {
    }

    public SettingsJsonStorage(string settingsFile)
    {
        _settingsFile = settingsFile;
    }

    private static string GetDefaultPath()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "codemade.imageviewer_settings.json");
        }

        // ApplicationData is ~/.config (or $XDG_CONFIG_HOME) on Linux, and ~/Library/Application Support on macOS
        var directory = OperatingSystem.IsMacOS() ? "ImageViewer" : "imageviewer";
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), directory, "settings.json");
    }

    public async Task<Settings> LoadSettings()
    {
        if (!File.Exists(_settingsFile))
        {
            return new Settings();
        }

        var json = await File.ReadAllTextAsync(_settingsFile).ConfigureAwait(false);
        return JsonSerializer.Deserialize(json, AppJsonContext.Default.Settings)
               ?? throw new JsonException("The settings file is empty");
    }

    public async Task SaveSettings(Settings settings)
    {
        var json = JsonSerializer.Serialize(settings, AppJsonContext.Default.Settings);
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsFile)!);
        await File.WriteAllTextAsync(_settingsFile, json).ConfigureAwait(false);
    }
}
