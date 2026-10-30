using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Platform;

namespace ImageViewer;

public sealed class WindowsPlatform : ITrash, IClickThrough, IWindowOpacity
{
    private const uint LWA_ALPHA = 2;

    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TRANSPARENT = 0x20;
    private const long WS_EX_LAYERED = 0x80000;
    private const uint FO_DELETE = 3;
    private const ushort FOF_ALLOWUNDO = 0x40;

    public Task MoveToTrash(string fileName)
    {
        // The path list ends with an extra null
        var from = Marshal.StringToHGlobalUni(Path.GetFullPath(fileName) + '\0');
        try
        {
            var operation = new SHFILEOPSTRUCT { wFunc = FO_DELETE, pFrom = from, fFlags = FOF_ALLOWUNDO };
            var result = SHFileOperationW(ref operation);
            if (operation.fAnyOperationsAborted != 0)
                throw new OperationCanceledException();
            if (result != 0)
                throw new Win32Exception(result);
        }
        finally
        {
            Marshal.FreeHGlobal(from);
        }
        return Task.CompletedTask;
    }

    public bool TrySet(IPlatformHandle window, bool enabled)
    {
        var style = GetWindowLongPtrW(window.Handle, GWL_EXSTYLE).ToInt64();
        var flags = WS_EX_LAYERED | WS_EX_TRANSPARENT;
        // Windows keeps the layered style if it had it, and it's what Opacity relies on
        var updated = enabled ? style | flags : style & ~WS_EX_TRANSPARENT;
        Marshal.SetLastPInvokeError(0);
        return SetWindowLongPtrW(window.Handle, GWL_EXSTYLE, new IntPtr(updated)) != IntPtr.Zero
               || Marshal.GetLastPInvokeError() == 0;
    }

    public bool TrySet(IPlatformHandle window, double opacity)
    {
        var style = GetWindowLongPtrW(window.Handle, GWL_EXSTYLE).ToInt64();
        SetWindowLongPtrW(window.Handle, GWL_EXSTYLE, new IntPtr(style | WS_EX_LAYERED));
        return SetLayeredWindowAttributes(window.Handle, 0, (byte)Math.Round(opacity * 255), LWA_ALPHA);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public IntPtr pFrom;
        public IntPtr pTo;
        public ushort fFlags;
        public int fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public IntPtr lpszProgressTitle;
    }

    [DllImport("shell32.dll")]
    private static extern int SHFileOperationW(ref SHFILEOPSTRUCT operation);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint colorKey, byte alpha, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtrW(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtrW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}
