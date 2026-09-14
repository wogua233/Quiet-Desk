using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Threading;

namespace QuietDesk;

// Opt-in release acceptance mode. Normal launches never start playback or telemetry.
internal sealed class PerformanceRecorder : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct Counters{public uint cb,PageFaultCount;public nuint PeakWorkingSetSize,WorkingSetSize,QuotaPeakPagedPoolUsage,QuotaPagedPoolUsage,QuotaPeakNonPagedPoolUsage,QuotaNonPagedPoolUsage,PagefileUsage,PeakPagefileUsage,PrivateUsage,PrivateWorkingSetSize,SharedCommitUsage;}
    [DllImport("psapi.dll")] private static extern bool GetProcessMemoryInfo(nint process,out Counters counters,uint size);
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(1)};
    private readonly Stopwatch watch=Stopwatch.StartNew();private readonly Process process=Process.GetCurrentProcess();
    private readonly List<object> samples=new();private readonly string file;private readonly int seconds,channels;private readonly PlayerModel model;
    private TimeSpan previousCpu;private double previous;private readonly Action done;
    private readonly Dictionary<int,TimeSpan> decoderCpu=new();
    internal PerformanceRecorder(PlayerModel model,string file,int seconds,int channels,Action done)
    {this.model=model;this.file=file;this.seconds=seconds;this.channels=channels;this.done=done;previousCpu=process.TotalProcessorTime;timer.Tick+=(_,_)=>Tick();timer.Start();}
    private void Tick()
    {
        process.Refresh();var now=watch.Elapsed.TotalSeconds;var cpu=process.TotalProcessorTime;
        double cpuSeconds=(cpu-previousCpu).TotalSeconds;long childWorking=0,childPrivate=0,childCommit=0;int children=0;
        foreach(var id in MediaSession.DecoderProcesses.Keys)try{using var child=Process.GetProcessById(id);var total=child.TotalProcessorTime;cpuSeconds+=(total-decoderCpu.GetValueOrDefault(id)).TotalSeconds;decoderCpu[id]=total;GetProcessMemoryInfo(child.Handle,out var cm,(uint)Marshal.SizeOf<Counters>());childWorking+=child.WorkingSet64;childPrivate+=(long)cm.PrivateWorkingSetSize;childCommit+=child.PrivateMemorySize64;children++;}catch(ArgumentException){}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}
        var percent=cpuSeconds/(now-previous)/Environment.ProcessorCount*100;previousCpu=cpu;previous=now;
        GetProcessMemoryInfo(process.Handle,out var memory,(uint)Marshal.SizeOf<Counters>());
        samples.Add(new{seconds=now,cpuPercent=percent,workingSet=process.WorkingSet64+childWorking,privateBytes=process.PrivateMemorySize64+childCommit,privateWorkingSet=(long)memory.PrivateWorkingSetSize+childPrivate,decoderProcesses=children,decoderPrivateWorkingSet=childPrivate,playing=model.Playing,activeChannels=model.Channels.Count(c=>c.Enabled)});
        bool complete=now>=seconds+60;
        bool saved=false;
        if(samples.Count%10==0||complete){try{var temp=file+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(new{status=complete?"complete":"running",mode="full WPF app, main window hidden, embedded desktop card, muted master",warmupSeconds=60,channels,samples}));File.Move(temp,file,true);saved=true;}catch(IOException){}catch(UnauthorizedAccessException){}}
        if(complete&&saved){timer.Stop();done();}
    }
    public void Dispose(){timer.Stop();process.Dispose();}
}
