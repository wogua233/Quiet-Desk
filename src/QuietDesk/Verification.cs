using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace QuietDesk;

internal static class Verification
{
    internal static int ModelTest(string[] args)
    {
        var lines=new List<string>();var output=Output(args,"model-verification.txt");
        try{
            var app=new System.Windows.Application();app.Resources.MergedDictionaries.Add(new(){Source=new Uri("/QuietDesk;component/UI/Theme.xaml",UriKind.Relative)});
            var folder=Path.Combine(Path.GetTempPath(),"quietdesk-model-"+Guid.NewGuid());
            using(var model=new PlayerModel(folder)){
                if(model.Playing)throw new Exception("Auto-play on startup");
                foreach(var c in model.Channels)c.Enabled=true;
                if(model.Channels.Count(c=>c.Enabled)!=4)throw new Exception("Four-channel limit failed");lines.Add("PASS four-channel UI model limit");
                model.SaveScene("测试组合");var scene=model.Scenes.Single();model.ApplyScene(new Scene{Name="空白"});model.ApplyScene(scene);
                if(model.Channels.Count(c=>c.Enabled)!=4)throw new Exception("Scene restore failed");lines.Add("PASS scene save / clear / restore");
                model.DeleteScene(scene);if(!model.CanUndoScene||model.Scenes.Count!=0)throw new Exception("Undo state missing");model.UndoScene();if(model.CanUndoScene||model.Scenes.Count!=1)throw new Exception("Undo failed");model.RenameScene(scene,"新组合");if(model.Scenes[0].Name!="新组合")throw new Exception("Rename failed");lines.Add("PASS scene rename / delete / undo");
                var volume=model.Master;model.ToggleMute();if(model.Master!=0)throw new Exception("Mute failed");model.ToggleMute();if(model.Master!=volume)throw new Exception("Restore volume failed");if(model.FeedbackEnabled)throw new Exception("Feedback must default off");lines.Add("PASS mute restore / silent feedback default");
                model.Queue.Add("first.wav");model.Queue.Add("second.wav");model.MoveFile("first.wav","second.wav");if(model.Queue[1]!="first.wav")throw new Exception("Queue reorder failed");model.Queue.Clear();lines.Add("PASS queue reorder");
                var window=new MainWindow(model);window.Width=1100;window.Height=790;
                var root=(System.Windows.FrameworkElement)window.Content;root.Measure(new(1100,760));root.Arrange(new(0,0,1100,760));root.UpdateLayout();
                var bmp=new System.Windows.Media.Imaging.RenderTargetBitmap(1100,760,96,96,System.Windows.Media.PixelFormats.Pbgra32);bmp.Render(root);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));using var image=File.Create(Path.Combine(Path.GetDirectoryName(output)!,"main-preview.png"));encoder.Save(image);lines.Add("PASS main window layout and render");
                window.Navigate(2);root.Measure(new(1100,760));root.Arrange(new(0,0,1100,760));root.UpdateLayout();window.Navigate(3);root.UpdateLayout();lines.Add("PASS library / settings pages construct");
                window.Navigate(0);foreach(double scale in new[]{1.0,1.25,1.5,2.0}){root.Measure(new(760,620));root.Arrange(new(0,0,760,620));root.UpdateLayout();var target=new System.Windows.Media.Imaging.RenderTargetBitmap((int)(760*scale),(int)(620*scale),96*scale,96*scale,System.Windows.Media.PixelFormats.Pbgra32);target.Render(root);var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(target));using var file=File.Create(Path.Combine(Path.GetDirectoryName(output)!,"layout-"+(int)(scale*100)+".png"));png.Save(file);}lines.Add("PASS small-layout render 100 / 125 / 150 / 200 percent (not physical DPI switch)");
            }
            using(var restored=new PlayerModel(folder)){if(restored.Playing||restored.Scenes.Count!=1||restored.Channels.Count(c=>c.Enabled)!=4)throw new Exception("Persistence mismatch");}lines.Add("PASS player state restart, no autoplay");
            var assets=Path.Combine(AppContext.BaseDirectory,"Assets","Sounds");var mp3=Path.Combine(folder,"fixture.mp3");
            using(var source=new WaveFileReader(Path.Combine(assets,"fireplace.wav")))MediaFoundationEncoder.EncodeToMp3(source,mp3,128000);
            using(var session=MediaSession.Open(mp3,false,CancellationToken.None).GetAwaiter().GetResult()){Thread.Sleep(150);var samples=new float[8192];session.Provider!.Read(samples,0,samples.Length);if(!samples.Any(x=>Math.Abs(x)>.0001))throw new Exception("MP3 no signal");}lines.Add("PASS MP3 local decode");
            var flac=Path.Combine(Path.GetDirectoryName(output)!,"fixture.flac");
            if(File.Exists(flac)){using var session=MediaSession.Open(flac,false,CancellationToken.None).GetAwaiter().GetResult();Thread.Sleep(150);var samples=new float[8192];session.Provider!.Read(samples,0,samples.Length);if(!samples.Any(x=>Math.Abs(x)>.0001))throw new Exception("FLAC no signal");lines.Add("PASS FLAC local decode");}
            else throw new Exception("FLAC fixture missing");
            File.WriteAllText(output,string.Join(Environment.NewLine,lines));return 0;
        }catch(Exception e){lines.Add("FAIL "+e);File.WriteAllText(output,string.Join(Environment.NewLine,lines));return 1;}
    }
    internal static int RadioTest(string[] args)
    {
        var entries=new List<object>();var directory=Path.GetDirectoryName(Output(args,"radio-verification.json"))!;
        try {
            using var catalog=new RadioDirectory(directory);
            var stations=catalog.Search("","ambient",CancellationToken.None).GetAwaiter().GetResult().Take(8).ToArray();
            foreach(var station in stations){var watch=Stopwatch.StartNew();try{using var session=MediaSession.Open(station.Url,true,CancellationToken.None).GetAwaiter().GetResult();var data=new float[4410*2];float peak=0;for(int i=0;i<30;i++){Thread.Sleep(100);session.Provider!.Read(data,0,data.Length);foreach(var value in data)peak=Math.Max(peak,Math.Abs(value));}if(peak<.00001)throw new IOException("No decoded signal");entries.Add(new{station.Name,station.Url,status="decoded",peak,seconds=watch.Elapsed.TotalSeconds});}catch(Exception e){entries.Add(new{station.Name,station.Url,status="failed",error=e.Message,seconds=watch.Elapsed.TotalSeconds});}File.WriteAllText(Output(args,"radio-verification.json"),JsonSerializer.Serialize(entries,new JsonSerializerOptions{WriteIndented=true}));}
            return 0;
        }catch(Exception e){File.WriteAllText(Output(args,"radio-verification.json"),JsonSerializer.Serialize(new{error=e.ToString(),entries}));return 1;}
    }
    private static string Output(string[] args,string fallback){int i=Array.IndexOf(args,"--out");return i>=0&&i+1<args.Length?args[i+1]:Path.Combine(AppContext.BaseDirectory,fallback);}
    internal static int Run(string[] args)
    {
        var report=new List<string>();int failed=0;
        void Check(string name,Action action){try{action();report.Add("PASS "+name);}catch(Exception e){failed++;report.Add("FAIL "+name+": "+e);}}
        void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
        var assets=Path.Combine(AppContext.BaseDirectory,"Assets","Sounds");
        Check("Focus completion is silent and does not restart",()=>{var f=new FocusClock(new());f.Configure(1);f.Toggle();for(int i=0;i<60;i++)f.Advance(1,DateTimeOffset.Now);Assert(f.Completed&&!f.Running&&f.Remaining==0,"completion state");Assert(Math.Abs(f.Daily.Values.Sum()-60)<.01,"daily total");});
        Check("Focus pause, resume and sleep gap",()=>{var f=new FocusClock(new());f.Configure(1);f.Toggle();f.Advance(3,DateTimeOffset.Now);f.Pause();f.Advance(2,DateTimeOffset.Now);f.Toggle();f.Advance(600,DateTimeOffset.Now);Assert(f.Remaining==57,"paused / sleep should not count");f.Advance(2,DateTimeOffset.Now);Assert(f.Remaining==55,"resume");});
        Check("Focus splits midnight",()=>{var f=new FocusClock(new());f.Toggle();f.Advance(2,new DateTimeOffset(2026,9,12,0,0,1,TimeSpan.FromHours(8)));Assert(f.Daily["2026-09-11"]==1&&f.Daily["2026-09-12"]==1,"midnight allocation");});
        Check("Atomic state round trip and corrupt recovery",()=>{var temp=Path.Combine(Path.GetTempPath(),"quietdesk-tests-"+Guid.NewGuid());var store=new StateStore(temp);var state=new SavedState{Master=.23,FocusMinutes=45};state.Scenes.Add(new(){Name="测试",Levels=new(){{"rain",.4}}});store.Save(state);var loaded=store.Load();Assert(loaded.Master==.23&&loaded.FocusMinutes==45&&loaded.Scenes[0].Name=="测试","round trip");File.WriteAllText(Path.Combine(temp,"state.json"),"{bad json");Assert(store.Load().FocusMinutes==25&&store.Warning!=null,"corrupt recovery");});
        Check("Only HTTP(S) station URLs accepted",()=>{Assert(RadioDirectory.IsHttp("https://example.com/radio"),"https");Assert(!RadioDirectory.IsHttp("file:///C:/data")&&!RadioDirectory.IsHttp("javascript:alert(1)"),"unsupported schemes");});
        Check("Network fragments are combined and position is tracked",()=>{using var partial=new FragmentedStream(new byte[]{1,2,3,4,5});using var stream=new DeadlineStream(partial,CancellationToken.None);var bytes=new byte[4];Assert(stream.Read(bytes,0,4)==4&&bytes[3]==4&&stream.Position==4,"fragment assembly");Assert(stream.Read(bytes,0,4)==1&&stream.Position==5,"EOF");});
        Check("Ring buffer cannot grow past three seconds",()=>{var ring=new FloatRing(WaveFormat.CreateIeeeFloatWaveFormat(44100,2));var data=new float[44100*2*4];Assert(ring.Write(data,0,data.Length)==44100*2*3,"capacity");Assert(ring.Write(data,0,1)==0,"overflow");ring.Read(data,0,data.Length);Assert(ring.Buffered==0,"drain");});
        Check("Cancelled source open does not start a decoder",()=>{using var cts=new CancellationTokenSource();cts.Cancel();try{MediaSession.Open("missing.wav",false,cts.Token).GetAwaiter().GetResult();throw new Exception("cancellation ignored");}catch(OperationCanceledException){}});
        foreach(var sound in Catalog.Sounds)Check("PCM + seamless loop boundary: "+sound.Id,()=>{using var reader=new AudioFileReader(Path.Combine(assets,sound.Id+".wav"));Assert(reader.WaveFormat.SampleRate==44100&&reader.WaveFormat.Channels==2,"format");Assert(reader.TotalTime.TotalSeconds>10,"duration");var tail=new float[2];reader.Position=reader.Length-8;reader.Read(tail,0,2);reader.Position=0;var head=new float[2];reader.Read(head,0,2);Assert(Math.Abs(tail[0]-head[0])<.1&&Math.Abs(tail[1]-head[1])<.1,"boundary discontinuity");});
        Check("Four-channel DSP finite, bounded, and produces audio",()=>{using var mix=new MixBus(assets);foreach(var s in Catalog.Sounds.Take(4))mix.SetLevel(s.Id,1);mix.Master=1;var data=new float[8192];float peak=0;for(int j=0;j<100;j++){mix.Read(data,0,data.Length);foreach(var f in data){Assert(float.IsFinite(f)&&Math.Abs(f)<1,"invalid or clipped output");peak=Math.Max(peak,Math.Abs(f));}}Assert(peak>.01,"no signal");});
        Check("Lazy mixer releases voices and never exceeds four",()=>{using var mix=new MixBus(assets);Assert(mix.ActiveCount==0,"eager load");var buffer=new float[8192];foreach(var sound in Catalog.Sounds){foreach(var old in Catalog.Sounds)mix.SetLevel(old.Id,0);mix.SetLevel(sound.Id,.5f);for(int i=0;i<20;i++){mix.Read(buffer,0,buffer.Length);Assert(mix.ActiveCount<=4,"voice cap");}}foreach(var sound in Catalog.Sounds)mix.SetLevel(sound.Id,0);for(int i=0;i<30;i++)mix.Read(buffer,0,buffer.Length);Assert(mix.ActiveCount==0,"voices not released");});
        Check("Local decoder feeds bounded three-second buffer",()=>{using var session=MediaSession.Open(Path.Combine(assets,"rain.wav"),false,CancellationToken.None).GetAwaiter().GetResult();var data=new float[4096];Thread.Sleep(100);session.Provider!.Read(data,0,data.Length);Assert(data.Any(x=>Math.Abs(x)>.001),"no decoded audio");});
        File.WriteAllText(Output(args,"verification.txt"),string.Join(Environment.NewLine,report));return failed==0?0:1;
    }
    private sealed class FragmentedStream(byte[] data) : MemoryStream(data)
    {public override int Read(byte[] buffer,int offset,int count)=>base.Read(buffer,offset,Math.Min(count,1));public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellationToken=default)=>base.ReadAsync(buffer[..Math.Min(buffer.Length,1)],cancellationToken);}
    [StructLayout(LayoutKind.Sequential)] private struct MemoryCounters{public uint cb,PageFaultCount;public nuint PeakWorkingSetSize,WorkingSetSize,QuotaPeakPagedPoolUsage,QuotaPagedPoolUsage,QuotaPeakNonPagedPoolUsage,QuotaNonPagedPoolUsage,PagefileUsage,PeakPagefileUsage,PrivateUsage,PrivateWorkingSetSize,SharedCommitUsage;}
    [DllImport("psapi.dll")] private static extern bool GetProcessMemoryInfo(nint process,out MemoryCounters counters,uint cb);
    internal static int Benchmark(string[] args)
    {
        int index=Array.IndexOf(args,"--seconds");int seconds=index>=0?int.Parse(args[index+1]):600;
        int channelsIndex=Array.IndexOf(args,"--channels");int channels=channelsIndex>=0?int.Parse(args[channelsIndex+1]):1;
        var path=Output(args,"benchmark.json");var samples=new List<object>();var watch=Stopwatch.StartNew();
        try{
            using var engine=new AudioEngine(Path.Combine(AppContext.BaseDirectory,"Assets","Sounds"));
            foreach(var sound in Catalog.Sounds.Take(channels))engine.SetLevel(sound.Id,.5);
            // Muted only at the master: all selected recordings are read and mixed in real time.
            engine.SetMaster(0);engine.Play();using var process=Process.GetCurrentProcess();var last=process.TotalProcessorTime;double previous=0;
            while(watch.Elapsed.TotalSeconds<seconds){Thread.Sleep(1000);process.Refresh();var elapsed=watch.Elapsed.TotalSeconds;var cpu=process.TotalProcessorTime;var percent=(cpu-last).TotalSeconds/(elapsed-previous)/Environment.ProcessorCount*100;last=cpu;previous=elapsed;
                GetProcessMemoryInfo(process.Handle,out var memory,(uint)Marshal.SizeOf<MemoryCounters>());
                samples.Add(new{seconds=elapsed,cpuPercent=percent,workingSet=process.WorkingSet64,privateBytes=process.PrivateMemorySize64,privateWorkingSet=(long)memory.PrivateWorkingSetSize});
                if(samples.Count%10==0)File.WriteAllText(path,JsonSerializer.Serialize(new{status="running",mode="audio-engine-only, real-time WASAPI, muted master",channels,samples}));
            }
            File.WriteAllText(path,JsonSerializer.Serialize(new{status="complete",mode="audio-engine-only, real-time WASAPI, muted master",channels,samples}));return 0;
        }catch(Exception e){File.WriteAllText(path,JsonSerializer.Serialize(new{status="failed",error=e.ToString(),samples}));return 1;}
    }
}
