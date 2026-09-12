using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace QuietDesk;

internal sealed partial class MediaSession : IDisposable
{
    private readonly CancellationTokenSource stop=new();private FloatRing? ring;
    private readonly TaskCompletionSource ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile bool ended;private int disposed;
    internal ISampleProvider? Provider {get;private set;}
    internal string? Error {get;private set;}
    internal bool Finished=>ended && (ring?.Buffered??0)==0;
    internal Task Worker {get;private set;}=Task.CompletedTask;
    private static readonly HttpClient Http=new(){Timeout=TimeSpan.FromSeconds(20)};
    internal static async Task<MediaSession> Open(string location,bool online,CancellationToken token,string format="MP3",bool direct=false)
    {
        token.ThrowIfCancellationRequested();
        var session=new MediaSession();bool external=online&&(format.Contains("HLS",StringComparison.OrdinalIgnoreCase)||format.Contains("AAC",StringComparison.OrdinalIgnoreCase)||location.Contains(".m3u8",StringComparison.OrdinalIgnoreCase)||location.Contains(".aac",StringComparison.OrdinalIgnoreCase));
        session.Worker=Task.Run(()=>external?session.ReadFfmpeg(location,direct):online?session.ReadRadio(location,direct):session.ReadFile(location));
        try {await session.ready.Task.WaitAsync(TimeSpan.FromSeconds(25),token);return session;}
        catch {session.Dispose();throw;}
    }
    private void Initialize(WaveFormat format)
    {
        ring=new FloatRing(format);ISampleProvider source=ring;
        if(format.Channels==1)source=new MonoToStereoSampleProvider(source);
        if(format.Channels>2)throw new NotSupportedException("首版支持单声道和立体声文件。");
        if(format.SampleRate!=44100)source=new WdlResamplingSampleProvider(source,44100);
        Provider=source;
    }
    private async Task Push(float[] samples,int length)
    {
        int offset=0;while(offset<length) {stop.Token.ThrowIfCancellationRequested();offset+=ring!.Write(samples,offset,length-offset);if(offset<length)await Task.Delay(20,stop.Token);}
        ready.TrySetResult();
    }
    private async Task ReadFile(string file)
    {
        try {using var reader=new AudioFileReader(file);Initialize(reader.WaveFormat);var data=new float[8192];int n;
            while((n=reader.Read(data,0,data.Length))>0)await Push(data,n);
            ready.TrySetResult();
        }catch(Exception e){Fail(e);}finally{ended=true;}
    }
    private async Task ReadRadio(string url,bool direct)
    {
        try {
            using var request=new HttpRequestMessage(HttpMethod.Get,url);request.Headers.UserAgent.ParseAdd("QuietDesk/0.1");
            using var directClient=direct?new HttpClient(new HttpClientHandler{UseProxy=false}){Timeout=TimeSpan.FromSeconds(20)}:null;
            using var response=await (directClient??Http).SendAsync(request,HttpCompletionOption.ResponseHeadersRead,stop.Token);response.EnsureSuccessStatusCode();
            await using var network=await response.Content.ReadAsStreamAsync(stop.Token);
            using var stream=new DeadlineStream(network,stop.Token);
            var frame=Mp3Frame.LoadFromStream(stream)??throw new IOException("未读到 MP3 音频帧；此地址可能是网页或不支持的流格式。");
            var mp3Format=new Mp3WaveFormat(frame.SampleRate,frame.ChannelMode==ChannelMode.Mono?1:2,frame.FrameLength,frame.BitRate);
            using var decoder=new AcmMp3FrameDecompressor(mp3Format);var format=decoder.OutputFormat;
            Initialize(WaveFormat.CreateIeeeFloatWaveFormat(format.SampleRate,format.Channels));
            byte[] pcm=new byte[65536];float[] floats=new float[32768];
            do {
                stop.Token.ThrowIfCancellationRequested();var bytes=decoder.DecompressFrame(frame,pcm,0);
                for(int i=0;i<bytes/2;i++)floats[i]=BitConverter.ToInt16(pcm,i*2)/32768f;
                await Push(floats,bytes/2);
                stream.ResetFrameBudget();
                frame=Mp3Frame.LoadFromStream(stream);
            }while(frame!=null);
        }catch(Exception e){Fail(e);}finally{ended=true;}
    }
    private void Fail(Exception e) {if(!stop.IsCancellationRequested)Error=e.Message;ready.TrySetException(e);}
    public void Dispose(){if(Interlocked.Exchange(ref disposed,1)==0){stop.Cancel();_ = Worker.ContinueWith(_=>stop.Dispose(),TaskScheduler.Default);}}
}

// MP3 frame parsing is synchronous. Every network read is still bounded and cancellable.
internal sealed class DeadlineStream : Stream
{
    private readonly Stream source;private readonly CancellationToken token;
    private long position;private int frameBytes;
    internal DeadlineStream(Stream source,CancellationToken token){this.source=source;this.token=token;}
    public override int Read(byte[] buffer,int offset,int count)
    {
        if(frameBytes+count>65536)throw new InvalidDataException("没有找到有效 MP3 音频帧，请使用电台直链。");
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(15));
        int total=0;while(total<count){var n=source.ReadAsync(buffer.AsMemory(offset+total,count-total),deadline.Token).AsTask().GetAwaiter().GetResult();if(n==0)break;total+=n;}
        position+=total;frameBytes+=total;return total;
    }
    internal void ResetFrameBudget()=>frameBytes=0;
    public override int ReadByte(){var one=new byte[1];return Read(one,0,1)==0?-1:one[0];}
    public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;
    public override long Length=>throw new NotSupportedException();public override long Position{get=>position;set=>throw new NotSupportedException();}
    public override void Flush(){}public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long value)=>throw new NotSupportedException();public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
}
