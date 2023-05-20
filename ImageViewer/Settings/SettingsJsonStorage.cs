using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ImageViewer;

public class SettingsJsonStorage : ISettingsStorage
{
    private readonly string _settingsFile;

    public SettingsJsonStorage()
    {
        _settingsFile = Environment.ExpandEnvironmentVariables("%userprofile%\\codemade.imageviewer_settings.json");
    }
    
    public async Task<Settings> LoadSettings()
    {
        if (!File.Exists(_settingsFile))
        {
            return new Settings();
        }
        
        using var fs = new FileStream(_settingsFile, FileMode.Open);
        using var textReader = new StreamReader(fs, Encoding.UTF8);
        var json = await textReader.ReadToEndAsync().ConfigureAwait(false);
        
        return JsonConvert.DeserializeObject<Settings>(json)!;
    }

    public async Task SaveSettings(Settings settings)
    {
        var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
        using var fs = new FileStream(_settingsFile, FileMode.Create);
        using var writer = new StreamWriter(fs, Encoding.UTF8);
        await writer.WriteAsync(json).ConfigureAwait(false);
    }
}