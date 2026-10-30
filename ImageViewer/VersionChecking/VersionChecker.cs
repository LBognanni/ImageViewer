using System;
using System.Threading.Tasks;

namespace ImageViewer;

public class VersionChecker
{
    private readonly IVersionGetter _versionGetter;
    private readonly Version _currentVersion;
    private readonly IUpdateNotifier _notifier;
    private readonly ITempData _tempDataProvider;
    internal const string LASTNOTIFICATIONDATE = "LastVersionNotificationTime";

    public VersionChecker(IVersionGetter versionGetter, Version currentVersion, IUpdateNotifier notifier, ITempData tempDataProvider)
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

        var lastNotification = _tempDataProvider.ReadTime(LASTNOTIFICATIONDATE);
        if (lastNotification > DateTime.Now.AddDays(-1))
            return;

        _notifier.Notify($"Image Viewer {newVersion} is available (you have {_currentVersion}). Press Enter to open the release page.", _versionGetter.GetReleaseUrl(newVersion));
        _tempDataProvider.WriteTime(LASTNOTIFICATIONDATE, DateTime.Now);
    }
}
