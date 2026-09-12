using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;
namespace QuietDesk;
internal sealed class InteractionFeedback : IDisposable
{
 private readonly SemaphoreSlim serial=new(1,1);private CancellationTokenSource? current;private long last;private bool disposed;
 internal static float[] Samples(double level,bool success){int n=success?7056:4410;var data=new float[n*2];for(int i=0;i<n;i++){double t=i/44100.0;double envelope=Math.Sin(Math.PI*i/n)*Math.Exp(-t*16);float v=(float)((Math.Sin(2*Math.PI*(success?740:520)*t)+.2*Math.Sin(2*Math.PI*1040*t))*envelope*Math.Clamp(level,0,1)*.45);data[i*2]=data[i*2+1]=v;}return data;}
 internal void Play(double level,string? deviceId,bool success,Action<string> error)
 {
  if(disposed||level<=0||(!success&&Environment.TickCount64-last<120))return;last=Environment.TickCount64;try{current?.Cancel();}catch(ObjectDisposedException){}var cts=new CancellationTokenSource();current=cts;
  _=Task.Run(async()=>{bool entered=false;try{await serial.WaitAsync(cts.Token);entered=true;cts.Token.ThrowIfCancellationRequested();using var enumerator=new MMDeviceEnumerator();using var device=deviceId==null?enumerator.GetDefaultAudioEndpoint(DataFlow.Render,Role.Console):enumerator.GetDevice(deviceId);var samples=Samples(level,success);byte[] bytes=new byte[samples.Length*4];Buffer.BlockCopy(samples,0,bytes,0,bytes.Length);using var stream=new RawSourceWaveStream(new MemoryStream(bytes),WaveFormat.CreateIeeeFloatWaveFormat(44100,2));using var output=new WasapiOut(device,AudioClientShareMode.Shared,true,40);var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);output.PlaybackStopped+=(_,e)=>{if(e.Exception!=null)completion.TrySetException(e.Exception);else completion.TrySetResult();};output.Init(stream);output.Play();try{await completion.Task.WaitAsync(TimeSpan.FromSeconds(2),cts.Token);}finally{output.Stop();}}
   catch(OperationCanceledException){}catch(Exception e){if(!disposed)error("轻音效输出失败："+e.Message);}finally{if(entered)serial.Release();cts.Dispose();}});
 }
 public void Dispose(){disposed=true;try{current?.Cancel();}catch(ObjectDisposedException){}current=null;}
}
