using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms=System.Windows.Forms;

namespace QuietDesk.DesktopProbe;

internal sealed class Placement
{
    public int? X { get; set; }
    public int? Y { get; set; }
    public bool Locked { get; set; }
}

internal sealed class DesktopHost : IDisposable
{
    private HwndSource? source;
    private readonly Dispatcher dispatcher;
    private readonly DispatcherTimer recovery;
    private readonly Action<string> report;
    private readonly string placementPath;
    private Placement placement;
    private Native.Point dragStart;
    private Native.Rect dragRect;
    private bool dragging, disposed;
    private int attempts;
    private TextBlock? feedback;
    internal nint Handle => source is { IsDisposed: false } ? source.Handle : 0;
    internal int Interactions { get; private set; }
    internal int Attachments { get; private set; }

    internal DesktopHost(Action<string> report, string directory)
    {
        this.report=report; dispatcher=Dispatcher.CurrentDispatcher;
        placementPath=Path.Combine(directory,"placement.json");
        try { placement=JsonSerializer.Deserialize<Placement>(File.ReadAllText(placementPath)) ?? new(); }
        catch (Exception e) when (e is IOException or JsonException) { placement=new(); }
        recovery=new DispatcherTimer(TimeSpan.FromSeconds(3),DispatcherPriority.Background,(_,_)=>CheckParent(),dispatcher);
        SystemEvents.DisplaySettingsChanged+=DisplayChanged;
        SystemEvents.SessionSwitch+=SessionChanged;
        Attach();
    }

    internal bool Attach()
    {
        var parent=Native.FindDesktopView();
        if (parent==0) { report("尚未找到可见桌面宿主；不会创建悬浮窗。"); return false; }
        ReleaseSource();
        // Match the shell host's actual DPI context before creating the child HWND.
        var previous=Native.SetThreadDpiAwarenessContext(Native.GetWindowDpiAwarenessContext(parent));
        try
        {
            var scale=Native.GetDpiForWindow(parent)/96.0;
            var width=(int)Math.Round(220*scale); var height=(int)Math.Round(320*scale);
            var work=Forms.Screen.PrimaryScreen!.WorkingArea;
            var point=new Native.Point(placement.X ?? work.Right-width-24,placement.Y ?? work.Top+80);
            var area=Forms.Screen.FromPoint(new System.Drawing.Point(point.X,point.Y)).WorkingArea;
            point.X=Math.Clamp(point.X,area.Left,Math.Max(area.Left,area.Right-width));
            point.Y=Math.Clamp(point.Y,area.Top,Math.Max(area.Top,area.Bottom-height));
            Native.ScreenToClient(parent,ref point);
            source=new HwndSource(new HwndSourceParameters("静隅 · 桌面嵌入原型") {
                ParentWindow=parent, WindowStyle=unchecked((int)0x56000000), // CHILD | VISIBLE | CLIPSIBLINGS | CLIPCHILDREN
                ExtendedWindowStyle=0x08000000, // NOACTIVATE: no focus theft
                PositionX=point.X,PositionY=point.Y,Width=width,Height=height,
                UsesPerPixelOpacity=false
            });
            source.RootVisual=BuildCard();
            source.AddHook(WindowMessage);
            Native.SetWindowPos(source.Handle,0,point.X,point.Y,width,height,0x10|0x40);
            Attachments++; attempts=0;
            report($"已创建真正子窗口。HWND={source.Handle}; parent={Native.GetParent(source.Handle)} ({Native.Class(parent)}); DPI={Native.GetDpiForWindow(source.Handle)}");
            return Native.GetParent(source.Handle)==parent;
        }
        catch(Exception e) { report("嵌入失败："+e.Message); ReleaseSource(); return false; }
        finally { Native.SetThreadDpiAwarenessContext(previous); }
    }

    private FrameworkElement BuildCard()
    {
        var root=new Border { Width=220,Height=320,Background=Brush("#171B20"),BorderBrush=Brush("#333A40"),BorderThickness=new Thickness(1),Padding=new Thickness(18) };
        var stack=new StackPanel(); root.Child=stack;
        var title=new TextBlock { Text="静隅  /  QUIET DESK",FontSize=11,Foreground=Brush("#94ADAA"),Margin=new Thickness(0,0,0,20),Cursor=Cursors.SizeAll,Background=Brush("#171B20"),Padding=new Thickness(0,5,0,5) };
        title.MouseLeftButtonDown+=(_,e)=> { if(placement.Locked) return; Native.GetCursorPos(out dragStart); Native.GetWindowRect(Handle,out dragRect); dragging=title.CaptureMouse(); e.Handled=true; };
        title.MouseMove+=(_,_)=> { if(!dragging) return; Native.GetCursorPos(out var current); var p=new Native.Point(dragRect.Left+current.X-dragStart.X,dragRect.Top+current.Y-dragStart.Y); Native.ScreenToClient(Native.GetParent(Handle),ref p); Native.SetWindowPos(Handle,0,p.X,p.Y,0,0,0x1|0x4|0x10); };
        title.MouseLeftButtonUp+=(_,_)=> { if(!dragging) return; dragging=false; title.ReleaseMouseCapture(); ClampAndSave(); report("拖动完成，位置已保存。"); };
        title.LostMouseCapture+=(_,_)=>dragging=false;
        stack.Children.Add(title);
        stack.Children.Add(Text("留一处安静",22,"#ECEFEE",0,0,0,6));
        stack.Children.Add(Text("桌面嵌入验证 · 尚未接入音频",10,"#8E999F",0,0,0,18));
        var button=new Button { Content="测试桌面交互",Height=36,Background=Brush("#B0CCC1"),Foreground=Brush("#15251F"),BorderThickness=new Thickness(0),Cursor=Cursors.Hand,FontSize=12 };
        button.Click+=(_,_)=> { Interactions++; feedback!.Text=$"已响应 {Interactions} 次点击"; report($"桌面卡片交互成功，累计 {Interactions} 次。"); };
        stack.Children.Add(button);
        stack.Children.Add(Text("25:00",36,"#ECEFEE",0,16,0,0));
        stack.Children.Add(Text("静默专注 · 计时功能待接入",10,"#8E999F",0,0,0,12));
        var locked=new CheckBox { Content="锁定桌面位置",IsChecked=placement.Locked,Foreground=Brush("#BFC8CB"),FontSize=11 };
        locked.Checked+=(_,_)=> { placement.Locked=true; Save(); }; locked.Unchecked+=(_,_)=>{placement.Locked=false;Save();};
        stack.Children.Add(locked);
        feedback=Text("仅用于验证桌面宿主兼容性",10,"#7C8B90",0,12,0,0); stack.Children.Add(feedback);
        return root;
    }

    private void ClampAndSave()
    {
        if(Handle==0 || !Native.GetWindowRect(Handle,out var rect)) return;
        var area=Forms.Screen.FromRectangle(new System.Drawing.Rectangle(rect.Left,rect.Top,rect.Right-rect.Left,rect.Bottom-rect.Top)).WorkingArea;
        placement.X=Math.Clamp(rect.Left,area.Left,Math.Max(area.Left,area.Right-(rect.Right-rect.Left)));
        placement.Y=Math.Clamp(rect.Top,area.Top,Math.Max(area.Top,area.Bottom-(rect.Bottom-rect.Top)));
        var p=new Native.Point(placement.X.Value,placement.Y.Value); Native.ScreenToClient(Native.GetParent(Handle),ref p);
        Native.SetWindowPos(Handle,0,p.X,p.Y,0,0,0x1|0x4|0x10); Save();
    }
    private void Save() { try { var tmp=placementPath+".tmp"; File.WriteAllText(tmp,JsonSerializer.Serialize(placement)); File.Move(tmp,placementPath,true); } catch(IOException e) {report("保存位置失败："+e.Message);} }
    private nint WindowMessage(nint hwnd,int msg,nint wp,nint lp,ref bool handled)
    {
        if(msg==0x21) { handled=true; return 3; } // MA_NOACTIVATE
        if(msg==0x2E0) dispatcher.BeginInvoke(()=>{if(!disposed) Attach();});
        return 0;
    }
    private void CheckParent()
    {
        if(disposed) return;
        if(Handle!=0 && Native.IsWindow(Handle) && Native.GetParent(Handle)==Native.FindDesktopView()) return;
        if(++attempts<=5) Attach();
        else if(attempts==6) report("桌面恢复已停止自动重试，请点击重新嵌入；未降级为悬浮窗。");
    }
    private void DisplayChanged(object? s,EventArgs e)=>dispatcher.BeginInvoke(()=>{if(!disposed) Attach();});
    private void SessionChanged(object s,SessionSwitchEventArgs e) { if(e.Reason==SessionSwitchReason.SessionUnlock) dispatcher.BeginInvoke(()=>{if(!disposed) Attach();}); }
    internal string Inspect()
    {
        if(Handle==0) return "没有嵌入窗口。";
        Native.GetWindowRect(Handle,out var r);
        var hit=Native.WindowFromPoint(new Native.Point((r.Left+r.Right)/2,(r.Top+r.Bottom)/2));
        return $"HWND {Handle}\n父窗口 {Native.GetParent(Handle)} / {Native.Class(Native.GetParent(Handle))}\n有效 {Native.IsWindow(Handle)}；可见标记 {Native.IsWindowVisible(Handle)}\n区域 [{r.Left}, {r.Top}, {r.Right}, {r.Bottom}]\n屏幕中心命中 {hit} / {Native.Class(hit)}\n点击 {Interactions} 次；宿主建立 {Attachments} 次\n注意：可见标记不代表未被其他窗口遮挡。";
    }
    private void ReleaseSource() { if(source is null)return; if(!source.IsDisposed) {source.RemoveHook(WindowMessage); source.Dispose();} source=null; }
    public void Dispose() { disposed=true; recovery.Stop();SystemEvents.DisplaySettingsChanged-=DisplayChanged;SystemEvents.SessionSwitch-=SessionChanged;ReleaseSource(); }
    internal static SolidColorBrush Brush(string color) {var b=new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));b.Freeze();return b;}
    internal static TextBlock Text(string text,double size,string color,double l,double t,double r,double b)=>new(){Text=text,FontSize=size,Foreground=Brush(color),Margin=new Thickness(l,t,r,b)};
}
