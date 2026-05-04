using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using global::Avalonia.Controls;
using global::Avalonia.Platform;

namespace LiveSplit.Avalonia;

internal static class NativeMousePassThrough
{
    private const string HwndDescriptor = "HWND";
    private const string XidDescriptor = "XID";

    private static readonly Dictionary<IntPtr, IntPtr> OriginalWindowsStyles = [];

    public static bool TryApply(Window window, bool enabled)
    {
        if (window?.TryGetPlatformHandle() is not IPlatformHandle handle)
        {
            return false;
        }

        return TryApply(handle, enabled);
    }

    internal static bool SupportsPlatformHandleDescriptor(string descriptor)
        => string.Equals(descriptor, HwndDescriptor, StringComparison.OrdinalIgnoreCase)
            || string.Equals(descriptor, XidDescriptor, StringComparison.OrdinalIgnoreCase);

    internal static bool TryApply(IPlatformHandle handle, bool enabled)
    {
        if (handle.Handle == IntPtr.Zero || !SupportsPlatformHandleDescriptor(handle.HandleDescriptor))
        {
            return false;
        }

        try
        {
            if (string.Equals(handle.HandleDescriptor, HwndDescriptor, StringComparison.OrdinalIgnoreCase))
            {
                return WindowsMousePassThrough.TryApply(handle.Handle, enabled);
            }

            return X11MousePassThrough.TryApply(handle.Handle, enabled);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException)
        {
            return false;
        }
    }

    private static class WindowsMousePassThrough
    {
        private const int GwlExStyle = -20;
        private const long WsExTransparent = 0x00000020L;
        private const long WsExLayered = 0x00080000L;

        public static bool TryApply(IntPtr hwnd, bool enabled)
        {
            IntPtr current = GetWindowLongPtr(hwnd, GwlExStyle);
            if (current == IntPtr.Zero && Marshal.GetLastWin32Error() != 0)
            {
                return false;
            }

            long currentStyle = current.ToInt64();
            if (enabled)
            {
                OriginalWindowsStyles.TryAdd(hwnd, current);
                currentStyle |= WsExTransparent | WsExLayered;
            }
            else if (OriginalWindowsStyles.Remove(hwnd, out IntPtr original))
            {
                currentStyle = original.ToInt64();
            }
            else
            {
                currentStyle &= ~WsExTransparent;
            }

            SetLastError(0);
            IntPtr previous = SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(currentStyle));
            return previous != IntPtr.Zero || Marshal.GetLastWin32Error() == 0;
        }

        [DllImport("kernel32.dll")]
        private static extern void SetLastError(uint dwErrCode);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    }

    private static class X11MousePassThrough
    {
        private const int ShapeSet = 0;
        private const int ShapeInput = 2;

        public static bool TryApply(IntPtr xid, bool enabled)
        {
            IntPtr display = XOpenDisplay(IntPtr.Zero);
            if (display == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                if (enabled)
                {
                    XShapeCombineRectangles(display, xid, ShapeInput, 0, 0, IntPtr.Zero, 0, ShapeSet, 0);
                }
                else
                {
                    XShapeCombineMask(display, xid, ShapeInput, 0, 0, IntPtr.Zero, ShapeSet);
                }

                XFlush(display);
                return true;
            }
            finally
            {
                XCloseDisplay(display);
            }
        }

        [DllImport("libX11.so.6")]
        private static extern IntPtr XOpenDisplay(IntPtr displayName);

        [DllImport("libX11.so.6")]
        private static extern int XCloseDisplay(IntPtr display);

        [DllImport("libX11.so.6")]
        private static extern int XFlush(IntPtr display);

        [DllImport("libXext.so.6")]
        private static extern void XShapeCombineRectangles(
            IntPtr display,
            IntPtr dest,
            int destKind,
            int xOff,
            int yOff,
            IntPtr rectangles,
            int nRectangles,
            int op,
            int ordering);

        [DllImport("libXext.so.6")]
        private static extern void XShapeCombineMask(
            IntPtr display,
            IntPtr dest,
            int destKind,
            int xOff,
            int yOff,
            IntPtr src,
            int op);
    }
}
