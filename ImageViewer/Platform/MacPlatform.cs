using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Platform;

namespace ImageViewer;

public sealed class MacPlatform : ITrash, IClickThrough, IWindowOpacity
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";

    public Task MoveToTrash(string fileName)
    {
        var pool = Send(Class("NSAutoreleasePool"), Selector("new"));
        try
        {
            var path = NSString(Path.GetFullPath(fileName));
            var url = Send(Class("NSURL"), Selector("fileURLWithPath:"), path);
            var manager = Send(Class("NSFileManager"), Selector("defaultManager"));
            if (!SendTrash(manager, Selector("trashItemAtURL:resultingItemURL:error:"), url, IntPtr.Zero, IntPtr.Zero))
                throw new IOException($"Can't move {fileName} to the Trash");
        }
        finally
        {
            Send(pool, Selector("drain"));
        }
        return Task.CompletedTask;
    }

    public bool TrySet(IPlatformHandle window, bool enabled)
    {
        var nsWindow = GetWindow(window);
        if (nsWindow == IntPtr.Zero)
            return false;

        SendBool(nsWindow, Selector("setIgnoresMouseEvents:"), enabled);
        return true;
    }

    public bool TrySet(IPlatformHandle window, double opacity)
    {
        var nsWindow = GetWindow(window);
        if (nsWindow == IntPtr.Zero)
            return false;

        SendDouble(nsWindow, Selector("setAlphaValue:"), opacity);
        return true;
    }

    private static IntPtr GetWindow(IPlatformHandle window) => window.HandleDescriptor switch
    {
        "NSWindow" => window.Handle,
        "NSView" => Send(window.Handle, Selector("window")),
        _ => IntPtr.Zero
    };

    private static IntPtr NSString(string s)
    {
        var utf8 = Marshal.StringToCoTaskMemUTF8(s);
        try
        {
            return Send(Class("NSString"), Selector("stringWithUTF8String:"), utf8);
        }
        finally
        {
            Marshal.FreeCoTaskMem(utf8);
        }
    }

    private static IntPtr Class(string name) => objc_getClass(name);

    private static IntPtr Selector(string name) => sel_registerName(name);

    [DllImport(ObjC, CharSet = CharSet.Ansi)]
    private static extern IntPtr objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjC, CharSet = CharSet.Ansi)]
    private static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr target, IntPtr selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr target, IntPtr selector, IntPtr argument);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void SendBool(IntPtr target, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool argument);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void SendDouble(IntPtr target, IntPtr selector, double argument);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SendTrash(IntPtr target, IntPtr selector, IntPtr url, IntPtr result, IntPtr error);
}
