using System;
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

public interface IWindowOpacity
{
    /// <summary>
    /// Have the system compositor fade a whole window, without redrawing it. <c>Window.Opacity</c>
    /// won't do: it doesn't apply to the custom image drawing. Returns false if this system can't.
    /// </summary>
    bool TrySet(IPlatformHandle window, double opacity);
}

public static class Platform
{
    public static IWindowOpacity CreateWindowOpacity()
    {
        if (OperatingSystem.IsWindows())
            return new WindowsPlatform();
        if (OperatingSystem.IsMacOS())
            return new MacPlatform();
        return new X11WindowOpacity();
    }

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
}
