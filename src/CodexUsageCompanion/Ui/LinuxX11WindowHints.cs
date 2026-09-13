using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace CodexUsageCompanion.Ui;

/// <summary>
/// Avoids Mutter's large client shadow for the compact overlay when Avalonia
/// runs through XWayland, while retaining a transparent surface for real
/// rounded corners.
/// </summary>
internal static class LinuxX11WindowHints
{
    private const int Replace = 0;
    private static readonly IntPtr XAAtom = new(4);

    public static void TrySetUtilityWindowType(Window window)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var handle = window.TryGetPlatformHandle();
        if (handle is null || handle.Handle == IntPtr.Zero ||
            !string.Equals(handle.HandleDescriptor, "XID", StringComparison.Ordinal))
        {
            return;
        }

        var display = XOpenDisplay(IntPtr.Zero);
        if (display == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var windowType = XInternAtom(display, "_NET_WM_WINDOW_TYPE", false);
            var utilityType = XInternAtom(display, "_NET_WM_WINDOW_TYPE_UTILITY", false);
            if (windowType == IntPtr.Zero || utilityType == IntPtr.Zero)
            {
                return;
            }

            var data = Marshal.AllocHGlobal(IntPtr.Size);
            try
            {
                Marshal.WriteIntPtr(data, utilityType);
                XChangeProperty(
                    display, handle.Handle, windowType, XAAtom, 32, Replace, data, 1);
                XFlush(display);
            }
            finally
            {
                Marshal.FreeHGlobal(data);
            }
        }
        finally
        {
            XCloseDisplay(display);
        }
    }

    [DllImport("libX11.so.6")]
    private static extern IntPtr XOpenDisplay(IntPtr displayName);

    [DllImport("libX11.so.6", CharSet = CharSet.Ansi)]
    private static extern IntPtr XInternAtom(IntPtr display, string atomName, bool onlyIfExists);

    [DllImport("libX11.so.6")]
    private static extern int XChangeProperty(
        IntPtr display, IntPtr window, IntPtr property, IntPtr type,
        int format, int mode, IntPtr data, int elementCount);

    [DllImport("libX11.so.6")]
    private static extern int XFlush(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern int XCloseDisplay(IntPtr display);
}
