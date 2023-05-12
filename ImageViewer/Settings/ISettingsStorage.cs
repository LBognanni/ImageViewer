using System.Threading.Tasks;

namespace ImageViewer;

public interface ISettingsStorage
{
    Task<Settings> LoadSettings();
    Task SaveSettings(Settings settings);
}