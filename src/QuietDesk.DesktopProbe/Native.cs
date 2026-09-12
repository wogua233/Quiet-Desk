using System;
using System.Runtime.InteropServices;
using System.Text;

namespace QuietDesk.DesktopProbe;

internal static class Native
{
    internal delegate bool EnumProc(nint hwnd, nint param);
    [StructLayout(LayoutKind.Sequential)] internal struct Point { internal int X, Y; internal Point(int x, int y) { X=x; Y=y; } }
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { internal int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc callback, nint param);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern nint FindWindowEx(nint parent, nint after, string? cls, string? title);
    [DllImport("user32.dll")] internal static extern nint GetParent(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] internal static extern bool ScreenToClient(nint hwnd, ref Point point);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] internal static extern nint GetWindowDpiAwarenessContext(nint hwnd);
    [DllImport("user32.dll")] internal static extern nint SetThreadDpiAwarenessContext(nint context);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern int GetClassName(nint hwnd, StringBuilder name, int count);
    internal static string Class(nint hwnd) { var text=new StringBuilder(256); GetClassName(hwnd,text,text.Capacity); return text.ToString(); }

    // Use the actual interactive desktop view as the parent. No shell injection,
    // external window style changes, wallpaper manipulation or floating fallback.
    internal static nint FindDesktopView()
    {
        nint result=0;
        EnumWindows((window, _) => {
            var cls=Class(window);
            if (cls is not ("Progman" or "WorkerW")) return true;
            var view=FindWindowEx(window,0,"SHELLDLL_DefView",null);
            if (view != 0 && IsWindowVisible(view)) { result=view; return false; }
            return true;
        },0);
        return result;
    }
}
