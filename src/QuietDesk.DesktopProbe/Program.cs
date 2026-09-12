using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Forms=System.Windows.Forms;

namespace QuietDesk.DesktopProbe;

internal static class Program
{
    [STAThread] private static void Main()
    {
        var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        var directory=Path.Combine(AppContext.BaseDirectory,"probe-data"); Directory.CreateDirectory(directory);
        var log=new TextBox { IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Background=DesktopHost.Brush("#101418"),Foreground=DesktopHost.Brush("#ADBABF"),BorderThickness=new Thickness(0),Padding=new Thickness(14),FontSize=12 };
        void Report(string text) {var line=$"{DateTimeOffset.Now:HH:mm:ss}  {text}{Environment.NewLine}";log.AppendText(line);log.ScrollToEnd();File.AppendAllText(Path.Combine(directory,"probe.log"),line);}
        var window=new Window { Title="静隅 · 桌面嵌入验收",Width=640,Height=560,Background=DesktopHost.Brush("#191E23"),Foreground=Brushes.White,FontFamily=new FontFamily("Microsoft YaHei UI"),WindowStartupLocation=WindowStartupLocation.CenterScreen };
        var root=new DockPanel {Margin=new Thickness(24)};window.Content=root;
        var heading=new StackPanel(); DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
        heading.Children.Add(DesktopHost.Text("先让安静，留在桌面。",24,"#ECF1EE",0,0,0,8));
        heading.Children.Add(new TextBlock {Text="这是嵌入验收原型，不是完整播放器。右侧卡片必须属于桌面，不能是普通悬浮窗。请显示桌面后测试点击、拖动、遮挡与恢复。",Foreground=DesktopHost.Brush("#9BA9AE"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,18)});
        DesktopHost? host=null;
        var controls=new WrapPanel {Margin=new Thickness(0,0,0,16)};heading.Children.Add(controls);
        void Button(string label,Action action) {var b=new Button {Content=label,Padding=new Thickness(12,8,12,8),Margin=new Thickness(0,0,8,8),Background=DesktopHost.Brush("#B0CCC1"),Foreground=DesktopHost.Brush("#17251F"),BorderThickness=new Thickness(0)};b.Click+=(_,_)=>action();controls.Children.Add(b);}
        Button("检查嵌入状态",()=>Report(host!.Inspect()));
        Button("重新嵌入",()=>host!.Attach());
        Button("收起到托盘",()=>window.Hide());
        Button("退出原型",()=>app.Shutdown());
        root.Children.Add(log);
        var tray=new Forms.NotifyIcon { Icon=System.Drawing.SystemIcons.Application,Text="静隅 · 嵌入验证",Visible=true,ContextMenuStrip=new Forms.ContextMenuStrip() };
        tray.ContextMenuStrip.Items.Add("打开验收窗口",null,(_,_)=>{window.Show();window.Activate();});
        tray.ContextMenuStrip.Items.Add("退出原型",null,(_,_)=>app.Shutdown());
        tray.DoubleClick+=(_,_)=>{window.Show();window.Activate();};
        window.Closing+=(_,e)=>{e.Cancel=true;window.Hide();};
        app.Exit+=(_,_)=>{host?.Dispose();tray.Dispose();};
        window.Show(); host=new DesktopHost(Report,directory); Report(host.Inspect());
        app.Run();
    }
}
