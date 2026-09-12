using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;

namespace QuietDesk;
internal sealed partial class MediaSession
{
    internal static readonly ConcurrentDictionary<int,byte> DecoderProcesses=new();
    private async Task ReadFfmpeg(string url,bool direct)
    {
        Process? process=null;Task? errors=null;
        try {
            if(!RadioDirectory.IsHttp(url))throw new InvalidDataException("仅支持 HTTP(S) 直播地址。");
            var exe=Path.Combine(AppContext.BaseDirectory,"Assets","FFmpeg","ffmpeg.exe");
            if(!File.Exists(exe))throw new FileNotFoundException("未找到随包的 HLS/AAC 解码器。",exe);
            var start=new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(var arg in new[]{"-hide_banner","-loglevel","error","-nostdin","-threads","1","-rw_timeout","15000000","-tls_verify","1","-protocol_whitelist","http,https,tcp,tls,crypto","-probesize","131072","-analyzeduration","2000000","-i",url,"-map","0:a:0","-vn","-sn","-dn","-threads","1","-ac","2","-ar","44100","-f","f32le","pipe:1"})start.ArgumentList.Add(arg);
            if(direct){foreach(var key in new[]{"http_proxy","https_proxy","all_proxy","HTTP_PROXY","HTTPS_PROXY","ALL_PROXY"})start.Environment.Remove(key);}
            process=Process.Start(start)??throw new IOException("无法启动直播解码器。");DecoderProcesses.TryAdd(process.Id,0);
            var running=process;using var cancellation=stop.Token.Register(()=>{try{if(!running.HasExited)running.Kill(true);}catch(InvalidOperationException){} });
            // Drain stderr without retaining an unbounded transcript or signed stream URLs.
            errors=Task.Run(async()=>{var chars=new char[2048];while(await running.StandardError.ReadAsync(chars.AsMemory())>0){} });
            Initialize(WaveFormat.CreateIeeeFloatWaveFormat(44100,2));var bytes=new byte[32768+8];var samples=new float[8192];int carry=0;
            while(true){stop.Token.ThrowIfCancellationRequested();var n=await process.StandardOutput.BaseStream.ReadAsync(bytes.AsMemory(carry,32768-carry),stop.Token);if(n==0)break;int total=carry+n,aligned=total-total%8;Buffer.BlockCopy(bytes,0,samples,0,aligned);if(aligned>0)await Push(samples,aligned/4);carry=total-aligned;if(carry>0)Buffer.BlockCopy(bytes,aligned,bytes,0,carry);}
            await process.WaitForExitAsync(stop.Token);if(process.ExitCode!=0)throw new IOException("HLS/AAC 直播连接或解码失败，请稍后重试。");ready.TrySetResult();
        }catch(Exception e){Fail(e);}finally{if(process!=null){try{if(!process.HasExited)process.Kill(true);await process.WaitForExitAsync();}catch(InvalidOperationException){}DecoderProcesses.TryRemove(process.Id,out _);if(errors!=null)try{await errors;}catch(IOException){}process.Dispose();}ended=true;}
    }
}
