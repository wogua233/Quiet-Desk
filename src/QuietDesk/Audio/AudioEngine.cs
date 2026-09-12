using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace QuietDesk;

internal sealed class LoopSample : ISampleProvider,IDisposable
{
    private readonly AudioFileReader reader;
    internal float Target;private float gain;
    internal bool Silent => Target==0 && gain<.00001f;
    public WaveFormat WaveFormat=>reader.WaveFormat;
    internal LoopSample(string path)=>reader=new(path);
    public int Read(float[] buffer,int offset,int count)
    {
        if(Target==0 && gain<.00001f) {Array.Clear(buffer,offset,count);return count;}
        var total=0;
        while(total<count) {var n=reader.Read(buffer,offset+total,count-total);if(n==0) {reader.Position=0;n=reader.Read(buffer,offset+total,count-total);if(n==0)break;} total+=n;}
        for(var i=0;i<total;i++) {gain+=(Target-gain)*.00015f;buffer[offset+i]*=gain;}
        if(total<count)Array.Clear(buffer,offset+total,count-total);
        return count;
    }
    public void Dispose()=>reader.Dispose();
}

internal sealed class MixBus : ISampleProvider,IDisposable
{
    private readonly object gate=new();private readonly Dictionary<string,LoopSample> loops=new();
    private readonly Dictionary<string,float> targets=new();private readonly string assets;
    private readonly List<string> expired=new(4);
    internal int ActiveCount {get{lock(gate)return loops.Count;}}
    private float[] scratch=new float[8192];private ISampleProvider? media;private float masterGain,mediaGain;
    internal volatile float Master=.45f,MediaLevel=.6f;
    internal volatile bool ReadMedia=true;
    public WaveFormat WaveFormat {get;}=WaveFormat.CreateIeeeFloatWaveFormat(44100,2);
    internal MixBus(string assets) {this.assets=assets;}
    internal void SetLevel(string id,float value) {lock(gate){targets[id]=value;if(loops.TryGetValue(id,out var loop))loop.Target=value;}}
    internal void SetMedia(ISampleProvider? source) {lock(gate){media=source;mediaGain=0;}}
    public int Read(float[] buffer,int offset,int count)
    {
        lock(gate) {
            if(scratch.Length<count)Array.Resize(ref scratch,count);
            Array.Clear(buffer,offset,count);
            expired.Clear();foreach(var pair in loops)if(pair.Value.Silent)expired.Add(pair.Key);
            foreach(var key in expired){loops[key].Dispose();loops.Remove(key);}
            foreach(var pair in targets){if(loops.Count>=4)break;if(pair.Value>0&&!loops.ContainsKey(pair.Key))loops.Add(pair.Key,new LoopSample(Path.Combine(assets,pair.Key+".wav")){Target=pair.Value});}
            foreach(var loop in loops.Values) {
                loop.Read(scratch,0,count);for(int i=0;i<count;i++)buffer[offset+i]+=scratch[i]*.38f;
            }
            if(media!=null && ReadMedia) {var n=media.Read(scratch,0,count);for(int i=0;i<n;i++){mediaGain+=(MediaLevel-mediaGain)*.00015f;buffer[offset+i]+=scratch[i]*mediaGain;}}
            for(int i=0;i<count;i++) {masterGain+=(Master-masterGain)*.0004f;var x=buffer[offset+i]*masterGain;buffer[offset+i]=x/(1+Math.Abs(x));}
            return count;
        }
    }
    public void Dispose() {lock(gate){foreach(var loop in loops.Values)loop.Dispose();loops.Clear();}}
}

internal sealed class AudioEngine : IDisposable
{
    private string? deviceId;private NAudio.CoreAudioApi.MMDevice? device;
    private readonly MixBus bus;private WasapiOut? output;private int generation;private bool disposed;
    internal MediaSession? Media {get;private set;}
    internal bool Playing {get;private set;}
    internal float Volume=.45f;
    internal event Action<string>? Failed;
    internal AudioEngine(string assets,string? selectedDevice=null){bus=new(assets);deviceId=selectedDevice;}
    internal void SetMediaReadEnabled(bool value)=>bus.ReadMedia=value;
    internal void SetDevice(string? id){deviceId=id;ReopenOutput();}
    internal void SetLevel(string id,double volume)=>bus.SetLevel(id,(float)volume);
    internal void SetMaster(double value) {Volume=(float)value;if(Playing)bus.Master=Volume;}
    internal void SetMediaVolume(double value)=>bus.MediaLevel=(float)value;
    internal async Task FadeMedia(){bus.MediaLevel=0;await Task.Delay(180);}
    internal void Play()
    {
        if(disposed)return;Interlocked.Increment(ref generation);
        try {if(output==null){using var enumerator=new NAudio.CoreAudioApi.MMDeviceEnumerator();device=deviceId==null?enumerator.GetDefaultAudioEndpoint(NAudio.CoreAudioApi.DataFlow.Render,NAudio.CoreAudioApi.Role.Console):enumerator.GetDevice(deviceId);output=new WasapiOut(device,NAudio.CoreAudioApi.AudioClientShareMode.Shared,true,100);output.Init(bus);output.PlaybackStopped+=(_,e)=>{if(e.Exception!=null){Playing=false;Failed?.Invoke("音频输出已中断，请重新连接设备后点击播放。 "+e.Exception.Message);}};}
            Playing=true;bus.Master=Volume;output.Play();
        } catch(Exception e) {Playing=false;output?.Dispose();output=null;throw new InvalidOperationException("无法打开音频输出设备："+e.Message,e);}
    }
    internal async Task Pause()
    {
        var token=Interlocked.Increment(ref generation);Playing=false;bus.Master=0;
        await Task.Delay(220);if(token==generation && !disposed)output?.Pause();
    }
    internal void ReopenOutput() {output?.Dispose();output=null;device?.Dispose();device=null;if(Playing)Play();}
    internal void SetMedia(MediaSession? session) {bus.SetMedia(null);Media?.Dispose();Media=session;bus.ReadMedia=true;bus.SetMedia(session?.Provider);}
    public void Dispose(){disposed=true;Interlocked.Increment(ref generation);output?.Dispose();device?.Dispose();bus.SetMedia(null);Media?.Dispose();bus.Dispose();}
}

internal sealed class FloatRing : ISampleProvider
{
    private readonly object gate=new();private readonly float[] samples;private int head,tail,count;
    public WaveFormat WaveFormat {get;}
    internal bool Completed {get;set;}
    internal int Buffered {get{lock(gate)return count;}}
    internal FloatRing(WaveFormat format) {WaveFormat=format;samples=new float[format.SampleRate*format.Channels*3];}
    internal int Write(float[] data,int offset,int length) {lock(gate){int n=Math.Min(length,samples.Length-count);for(int i=0;i<n;i++){samples[tail]=data[offset+i];tail=(tail+1)%samples.Length;}count+=n;return n;}}
    public int Read(float[] data,int offset,int length) {lock(gate){int n=Math.Min(count,length);for(int i=0;i<n;i++){data[offset+i]=samples[head];head=(head+1)%samples.Length;}count-=n;Array.Clear(data,offset+n,length-n);return length;}}
}
