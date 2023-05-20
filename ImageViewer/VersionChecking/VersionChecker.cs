using System;
using System.Threading.Tasks;

namespace ImageViewer;

public class VersionChecker
{
    private readonly IVersionGetter _versionGetter;
    private readonly Version _currentVersion;
    private readonly IWindowsNotification _notifier;
    private readonly ITempData _tempDataProvider;
    internal const string LASTNOTIFICATIONDATE = "LastVersionNotificationTime"; 

    public VersionChecker(IVersionGetter versionGetter, Version currentVersion, IWindowsNotification notifier, ITempData tempDataProvider)
    {
        _versionGetter = versionGetter;
        _currentVersion = currentVersion;
        _notifier = notifier;
        _tempDataProvider = tempDataProvider;
    }
    
    public async Task NotifyIfNewVersion()
    {
        var newVersion = await _versionGetter.GetLatestVersion().ConfigureAwait(false) ?? _currentVersion;
        if (newVersion <= _currentVersion)
            return;

        var lastNotification = _tempDataProvider.Read<DateTime>(LASTNOTIFICATIONDATE);
        if (lastNotification > DateTime.Now.AddDays(-1))
            return;
        
        _notifier.Send("A new version of Image Viewer is available", $"You have version {_currentVersion}, click the button below to visit the latest release page", $"Download ImageViewer {newVersion}", _versionGetter.GetReleaseUrl(newVersion));
        _tempDataProvider.Write(LASTNOTIFICATIONDATE, DateTime.Now);
    }
}