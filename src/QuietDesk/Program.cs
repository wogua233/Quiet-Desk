using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using Forms=System.Windows.Forms;

namespace QuietDesk;

internal static class Program
{
    [STAThread] private static int Main(string[] args)
    {
        if(args.Contains("--signal-test"))return SignalVerification.Run(args);
        if(args.Contains("--scene-test"))return SceneSelectionVerification.Run(args);
        if(args.Contains("--desktop-test"))return RevisionVerification.Desktop(args);
        if(args.Contains("--revision-test"))return RevisionVerification.Run(args);
        if(args.Contains("--domestic-test"))return RevisionVerification.Domestic(args);
        if(args.Contains("--verify"))return Verification.Run(args);
        if(args.Contains("--radio-test"))return Verification.RadioTest(args);
        if(args.Contains("--model-test"))return Verification.ModelTest(args);
        if(args.Contains("--benchmark"))return Verification.Benchmark(args);
        using var instance=new Mutex(true,"Local\\QuietDesk.Player.v1",out var first);
        if(!first){MessageBox.Show("静隅已在运行，请双击系统托盘图标打开。","静隅");return 0;}
        var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/QuietDesk;component/UI/Theme.xaml",UriKind.Relative)});
        var directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"QuietDesk");
        PlayerModel? model=null;DesktopHost? desktop=null;Forms.NotifyIcon? tray=null;PerformanceRecorder? recorder=null;LocalAudioDiagnostic? diagnostic=null;
        bool acceptance=args.Contains("--acceptance-run");
        if(acceptance)directory=Path.Combine(Path.GetTempPath(),"QuietDesk-acceptance-"+Guid.NewGuid());
        try {
            model=new PlayerModel(directory);Ui.ClickFeedback=()=>model.Feedback();Ui.MotionReduced=()=>model.ReduceMotion;var window=new MainWindow(model);
            void Open(){window.Show();window.Activate();}
            window.ExitApp=()=>app.Shutdown();
            desktop=new DesktopHost(message=>{
                if(message.Contains("失败")||message.Contains("未找到")||message.Contains("停止自动"))model.Status=message;
                var log=Path.Combine(directory,"desktop.log");try{if(File.Exists(log)&&new FileInfo(log).Length>256_000)File.Move(log,log+".previous",true);File.AppendAllText(log,$"{DateTimeOffset.Now:O} {message}\n");}catch(IOException){}
            },directory,host=>DesktopCard.Create(model,host,Open));
            model.DesktopTransparencyAvailable=desktop.TransparencyAvailable;desktop.TransparencyChanged+=available=>model.DesktopTransparencyAvailable=available;
            window.ReattachDesktop=()=>desktop.Attach();
            tray=new Forms.NotifyIcon{Icon=new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory,"Assets","quietdesk.ico")),Visible=true,Text="静隅 · Quiet Desk",ContextMenuStrip=new Forms.ContextMenuStrip()};
            tray.DoubleClick+=(_,_)=>Open();tray.ContextMenuStrip.Items.Add("打开静隅",null,(_,_)=>Open());tray.ContextMenuStrip.Items.Add("播放 / 暂停",null,(_,_)=>_ = model.TogglePlay());tray.ContextMenuStrip.Items.Add("开始 / 暂停专注",null,(_,_)=>model.ToggleFocus());tray.ContextMenuStrip.Items.Add("重新嵌入桌面",null,(_,_)=>desktop.Attach());tray.ContextMenuStrip.Items.Add("退出",null,(_,_)=>app.Shutdown());
            int di=Array.IndexOf(args,"--audio-diagnostic-dir");
            if(di>=0&&di+1<args.Length)diagnostic=new LocalAudioDiagnostic(model,Path.GetFullPath(args[di+1]));
            window.Show();
            if(acceptance){int ci=Array.IndexOf(args,"--channels");int count=ci>=0?int.Parse(args[ci+1]):1;int oi=Array.IndexOf(args,"--out");string report=oi>=0?args[oi+1]:Path.Combine(AppContext.BaseDirectory,"full-app-performance.json");int si=Array.IndexOf(args,"--seconds");int seconds=si>=0?int.Parse(args[si+1]):600;
                model.ApplyScene(new Scene{Name="性能验收 · 静音播放",Levels=Catalog.Sounds.Take(count).ToDictionary(s=>s.Id,_=>.5)});model.Master=0;_ = model.TogglePlay();window.Hide();
                int ri=Array.IndexOf(args,"--acceptance-radio");if(ri>=0){var station=RadioDirectory.Domestic().First(s=>s.Format=="HLS/AAC");_ = model.PlayStation(station);}
                recorder=new PerformanceRecorder(model,report,seconds,count,()=>app.Shutdown());
            }
            app.Run();return 0;
        }catch(Exception e){Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"startup-error.log"),e.ToString());MessageBox.Show("静隅暂时无法启动：\n"+e.Message,"静隅");return 1;}
        finally{diagnostic?.Dispose();recorder?.Dispose();desktop?.Dispose();tray?.Icon?.Dispose();tray?.Dispose();model?.Dispose();}
    }
}
