using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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

    private static async Task<bool> TryRun(string program, params string[] arguments)
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
