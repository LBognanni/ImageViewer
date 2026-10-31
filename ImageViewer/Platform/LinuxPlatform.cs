using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Platform;

namespace ImageViewer;

public sealed class LinuxTrash : ITrash
{
    public async Task MoveToTrash(string fileName)
    {
        // Each of these knows about trash folders on other drives
        if (await TryRun("gio", "trash", fileName))
            return;
        if (await TryRun("kioclient", "move", fileName, "trash:/"))
            return;
        if (await TryRun("kioclient5", "move", fileName, "trash:/"))
            return;

        throw new IOException("No trash tool worked. Install gio or kioclient to delete files from here.");
    }

    internal static async Task<bool> TryRun(string program, params string[] arguments)
    {
        var start = new ProcessStartInfo(program) { RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(start);
            if (process == null)
                return false;
            await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            // Not installed
            return false;
        }
    }
}

/// <summary>
/// Installs a desktop entry for wherever this app is running from, and makes it the default for
/// the formats it can open. Other formats stay with whatever viewer handled them before.
/// </summary>
public sealed class LinuxDefaultViewer : IDefaultViewer
{
    private const string DesktopFile = "imageviewer.desktop";

    private static readonly string[] MimeTypes =
    {
        "image/bmp", "image/gif", "image/jpeg", "image/png", "image/webp", "image/vnd.microsoft.icon", "image/vnd.wap.wbmp"
    };

    public async Task<string> MakeDefault()
    {
        var data = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
        var applications = Path.Combine(data, "applications");
        var icons = Path.Combine(data, "icons", "hicolor", "512x512", "apps");
        Directory.CreateDirectory(applications);
        Directory.CreateDirectory(icons);

        await File.WriteAllTextAsync(Path.Combine(applications, DesktopFile), GetDesktopEntry(GetProgramPath()));

        await using (var icon = typeof(LinuxDefaultViewer).Assembly.GetManifestResourceStream("imageviewer.png")!)
        await using (var file = File.Create(Path.Combine(icons, "imageviewer.png")))
        {
            await icon.CopyToAsync(file);
        }

        if (!await LinuxTrash.TryRun("xdg-mime", ["default", DesktopFile, .. MimeTypes]))
            throw new IOException("Couldn't set the default viewer. Install xdg-utils and try again.");

        return "Image Viewer now opens BMP, GIF, ICO, JPEG, PNG, WBMP and WebP files. "
            + "If you move the app, set it as the default again.";
    }

    /// <summary>
    /// The AppImage runtime sets $APPIMAGE. The running binary is inside a mount that goes away on exit.
    /// </summary>
    internal static string GetProgramPath() =>
        Environment.GetEnvironmentVariable("APPIMAGE")
        ?? Environment.ProcessPath
        ?? throw new IOException("Couldn't find where the app is installed.");

    internal static string GetDesktopEntry(string program) => $"""
        [Desktop Entry]
        Type=Application
        Name=Image Viewer
        Comment=Fast and private image viewer
        Exec={QuoteForExec(program)} %f
        Icon=imageviewer
        Terminal=false
        Categories=Graphics;Viewer;
        MimeType={string.Join(';', MimeTypes)};

        """;

    /// <summary>
    /// Exec has its own quoting, and the whole value is then escaped again as a desktop entry string
    /// </summary>
    private static string QuoteForExec(string path)
    {
        var quoted = new StringBuilder("\"");
        foreach (var c in path)
        {
            if (c is '"' or '`' or '$' or '\\')
                quoted.Append('\\');
            quoted.Append(c);
        }
        quoted.Append('"');
        return quoted.ToString().Replace("\\", "\\\\").Replace("%", "%%");
    }
}

/// <summary>
/// An empty input region makes X11 pass all mouse events to whatever is underneath. Under a Wayland
/// session this runs through XWayland, and how it behaves is up to the compositor.
/// </summary>
public sealed class X11ClickThrough : IClickThrough
{
    private const int ShapeSet = 0;
    private const int ShapeInput = 2;

    public bool TrySet(IPlatformHandle window, bool enabled)
    {
        if (window.HandleDescriptor != "XID")
            return false;

        try
        {
            var display = XOpenDisplay(IntPtr.Zero);
            if (display == IntPtr.Zero)
                return false;

            try
            {
                if (enabled)
                {
                    XShapeCombineRectangles(display, window.Handle, ShapeInput, 0, 0, IntPtr.Zero, 0, ShapeSet, 0);
                }
                else
                {
                    XShapeCombineMask(display, window.Handle, ShapeInput, 0, 0, IntPtr.Zero, ShapeSet);
                }
                XSync(display, false);
                return true;
            }
            finally
            {
                XCloseDisplay(display);
            }
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    [DllImport("libX11.so.6")]
    private static extern IntPtr XOpenDisplay(IntPtr name);

    [DllImport("libX11.so.6")]
    private static extern int XCloseDisplay(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern int XSync(IntPtr display, [MarshalAs(UnmanagedType.I1)] bool discard);

    [DllImport("libXext.so.6")]
    private static extern void XShapeCombineRectangles(IntPtr display, IntPtr window, int kind, int x, int y,
        IntPtr rectangles, int count, int operation, int ordering);

    [DllImport("libXext.so.6")]
    private static extern void XShapeCombineMask(IntPtr display, IntPtr window, int kind, int x, int y, IntPtr mask, int operation);
}
