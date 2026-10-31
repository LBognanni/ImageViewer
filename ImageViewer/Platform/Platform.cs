using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform;

namespace ImageViewer;

public interface ITrash
{
    /// <summary>
    /// Move a file to the system trash. Never deletes it permanently: if the file
    /// can't be trashed this throws, and <see cref="OperationCanceledException"/>
    /// means the user declined.
    /// </summary>
    Task MoveToTrash(string fileName);
}

public interface IClickThrough
{
    /// <summary>
    /// Make a window ignore the mouse, or stop ignoring it. Returns false if this system can't.
    /// </summary>
    bool TrySet(IPlatformHandle window, bool enabled);
}

public interface IDefaultViewer
{
    /// <summary>
    /// Make this the system's image viewer, or as close as this system allows. Returns
    /// what to tell the user, and throws <see cref="IOException"/> if it failed.
    /// </summary>
    Task<string> MakeDefault();
}

public static class Platform
{
    public static ITrash CreateTrash()
    {
        if (OperatingSystem.IsWindows())
            return new WindowsPlatform();
        if (OperatingSystem.IsMacOS())
            return new MacPlatform();
        return new LinuxTrash();
    }

    public static IClickThrough CreateClickThrough()
    {
        if (OperatingSystem.IsWindows())
            return new WindowsPlatform();
        if (OperatingSystem.IsMacOS())
            return new MacPlatform();
        return new X11ClickThrough();
    }

    public static IDefaultViewer CreateDefaultViewer()
    {
        if (OperatingSystem.IsWindows())
            return new WindowsPlatform();
        if (OperatingSystem.IsMacOS())
            return new MacPlatform();
        return new LinuxDefaultViewer();
    }
}
