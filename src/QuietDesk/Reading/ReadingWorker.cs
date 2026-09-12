using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UglyToad.PdfPig;
namespace QuietDesk.Reading;
internal sealed class IdleGate
{
    [StructLayout(LayoutKind.Sequential)] private struct Input {public uint Size,Tick;}
    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref Input input);
    [DllImport("kernel32.dll")] private static extern bool GetSystemTimes(out long idle,out long kernel,out long user);
    [DllImport("shell32.dll")] private static extern int SHQueryUserNotificationState(out int state);
    private long lastIdle,lastKernel,lastUser;private DateTimeOffset lowSince=DateTimeOffset.Now;
    internal static bool CanRun(uint idleMilliseconds,double quietSeconds,bool ac,bool presentation)=>idleMilliseconds>=180000&&quietSeconds>=30&&ac&&!presentation;
    internal bool Allowed(){var i=new Input{Size=8};if(!GetLastInputInfo(ref i))return false;bool cpu=false;if(GetSystemTimes(out var idle,out var kernel,out var user)){long total=kernel-lastKernel+user-lastUser;cpu=lastKernel!=0&&total>0&&1.0-(idle-lastIdle)/(double)total<.2;lastIdle=idle;lastKernel=kernel;lastUser=user;}if(!cpu)lowSince=DateTimeOffset.Now;
        var power=System.Windows.Forms.SystemInformation.PowerStatus;bool ac=power.PowerLineStatus==System.Windows.Forms.PowerLineStatus.Online;
        bool presentation=SHQueryUserNotificationState(out var state)!=0||state is 2 or 3 or 4 or 6 or 7;
        return CanRun(unchecked((uint)Environment.TickCount-i.Tick),(DateTimeOffset.Now-lowSince).TotalSeconds,ac,presentation);
    }
}
internal sealed class WorkerBudget:IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct Basic {public long PerProcess,PerJob;public uint Flags;public UIntPtr Min,Max;public uint Active;public UIntPtr Affinity;public uint Priority,Scheduling;}
    [StructLayout(LayoutKind.Sequential)] private struct Io {public ulong A,B,C,D,E,F;}
    [StructLayout(LayoutKind.Sequential)] private struct Extended {public Basic Basic;public Io Io;public UIntPtr ProcessMemory,JobMemory,PeakProcess,PeakJob;}
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr CreateJobObject(IntPtr attributes,string? name);
    [DllImport("kernel32.dll")] private static extern bool SetInformationJobObject(IntPtr job,int info,ref Extended value,uint size);
    [DllImport("kernel32.dll")] private static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);
    private IntPtr handle;
    internal WorkerBudget(Process process){handle=CreateJobObject(IntPtr.Zero,null);var limits=new Extended{Basic=new Basic{Flags=0x2100},ProcessMemory=(UIntPtr)(256*1024*1024)};if(handle==IntPtr.Zero||!SetInformationJobObject(handle,9,ref limits,(uint)Marshal.SizeOf<Extended>())||!AssignProcessToJobObject(handle,process.Handle)){Dispose();throw new IOException("无法建立解析进程资源限制。");}}
    public void Dispose(){if(handle!=IntPtr.Zero){CloseHandle(handle);handle=IntPtr.Zero;}}
}
internal sealed class WorkerRequest {public string Url {get;set;}="";public string File {get;set;}="";public string Publisher {get;set;}="";}
internal static class ReadingWorker
{
    internal static int Run(string[] args){try{if(Console.ReadLine()!="go")return 1;var input=JsonSerializer.Deserialize<WorkerRequest>(File.ReadAllText(args[1]))!;var result=Extract(input,CancellationToken.None).GetAwaiter().GetResult();File.WriteAllText(args[2],JsonSerializer.Serialize(result));return 0;}catch(Exception e){string error=e is System.Net.Http.HttpRequestException h?$"需在浏览器访问：HTTP {h.StatusCode}。":e is IOException?e.Message:"全文解析失败，可能受权限、文件格式或资源限制影响。";File.WriteAllText(args[2],JsonSerializer.Serialize(new Extracted{Error=error}));return 1;}}
    private static async Task<Extracted> Extract(WorkerRequest request,CancellationToken ct){byte[] bytes;bool pdf=request.File.Length>0;
        if(pdf){var info=new FileInfo(request.File);if(info.Length>25*1024*1024)throw new IOException("PDF超过25MB上限。");bytes=await File.ReadAllBytesAsync(request.File,ct);}
        else{if(!ReadingCatalog.Http(request.Url))throw new IOException();using var client=ReadingContent.Client();string fetchUrl=request.Publisher=="aps"?request.Url.Replace("/abstract/","/pdf/"):request.Url;using var response=await client.GetAsync(fetchUrl,System.Net.Http.HttpCompletionOption.ResponseHeadersRead,ct);pdf=response.Content.Headers.ContentType?.MediaType=="application/pdf";bytes=await ReadingContent.Bounded(response,pdf?25*1024*1024:4*1024*1024,ct);}
        if(pdf||bytes.AsSpan().StartsWith("%PDF"u8)){using var doc=PdfDocument.Open(bytes);var text=new System.Text.StringBuilder();bool complete=true;int contentCharacters=0;foreach(var page in doc.GetPages()){var pageText=UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor.ContentOrderTextExtractor.GetText(page);if(pageText.Trim().Length<100){complete=false;continue;}contentCharacters+=pageText.Length;text.AppendLine($"\n【第{page.Number}页】\n{pageText}");if(text.Length>200000){complete=false;break;}}if(contentCharacters<200)return new(){Error="PDF没有足够的可提取文字；首版不执行OCR。"};return new(){Text=text.ToString(0,Math.Min(text.Length,200000)),Basis="PDF页码",Complete=complete};}
        return ReadingContent.ExtractHtml(System.Text.Encoding.UTF8.GetString(bytes),request.Publisher);
    }
    internal static readonly System.Collections.Concurrent.ConcurrentDictionary<int,byte> Processes=new();
    internal static async Task<Extracted> Launch(WorkerRequest request,string directory,CancellationToken ct){Directory.CreateDirectory(directory);string stem=Path.Combine(directory,Guid.NewGuid().ToString("N")),input=stem+".input",output=stem+".output";await File.WriteAllTextAsync(input,JsonSerializer.Serialize(request),ct);
        try{var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true};if(string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath),"dotnet",StringComparison.OrdinalIgnoreCase))start.ArgumentList.Add(typeof(ReadingWorker).Assembly.Location);start.ArgumentList.Add("--reading-worker");start.ArgumentList.Add(input);start.ArgumentList.Add(output);using var p=Process.Start(start)??throw new IOException("无法启动解析进程。");try{Processes[p.Id]=0;using var budget=new WorkerBudget(p);p.PriorityClass=ProcessPriorityClass.BelowNormal;await p.StandardInput.WriteLineAsync("go");p.StandardInput.Close();using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(90));await p.WaitForExitAsync(timeout.Token);if(!File.Exists(output))throw new IOException("解析进程达到资源上限或提前退出。");return JsonSerializer.Deserialize<Extracted>(await File.ReadAllTextAsync(output,ct))!;}finally{if(!p.HasExited){p.Kill(true);await p.WaitForExitAsync();}Processes.TryRemove(p.Id,out _);}}
        finally{File.Delete(input);File.Delete(output);}
    }
}
