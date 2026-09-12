using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace QuietDesk;
internal static class WindowChrome
{
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint hwnd,int attribute,ref int value,int size);
    internal static void DarkTitle(Window window)=>window.SourceInitialized+=(_,_)=>{int enabled=1;DwmSetWindowAttribute(new WindowInteropHelper(window).Handle,20,ref enabled,4);};
}
