using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
namespace QuietDesk.Reading;

// Explicit isolated verification only; never instantiates the subscription scheduler.
internal static class AiLiveVerification
{
    internal static async Task<int> Network(string[] args){
        var rows=new List<object>();
        foreach(bool tls12 in new[]{false,true}){
            using var handler=new System.Net.Http.HttpClientHandler{AllowAutoRedirect=false};
            if(tls12)handler.SslProtocols=System.Security.Authentication.SslProtocols.Tls12;
            using var client=new System.Net.Http.HttpClient(handler){Timeout=TimeSpan.FromSeconds(15)};
            using var request=new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get,"https://api.deepseek.com/models"){Version=System.Net.HttpVersion.Version20,VersionPolicy=System.Net.Http.HttpVersionPolicy.RequestVersionOrLower};
            try{using var response=await client.SendAsync(request);rows.Add(new{tls12,status=(int)response.StatusCode,version=response.Version.ToString()});}
            catch(Exception e){rows.Add(new{tls12,error=e.ToString()});}
        }
        File.WriteAllText(args[Array.IndexOf(args,"--out")+1],JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));return 0;
    }
    internal static async Task<int> Run(string[] args)
    {
        string Arg(string name)=>args[Array.IndexOf(args,name)+1];
        string configPath=Path.GetFullPath(Arg("--test-config")),output=Path.GetFullPath(Arg("--out"));
        string counter=output+".attempts";
        var rows=new List<object>();string[] models=Array.Empty<string>();string selected="",error="";
        int attempts=File.Exists(counter)?int.Parse(File.ReadAllText(counter)):0;
        var watch=Stopwatch.StartNew();using var process=Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;long peak=process.PrivateMemorySize64;
        using var sampleStop=new CancellationTokenSource();
        var sampler=Task.Run(async()=>{try{while(true){process.Refresh();peak=Math.Max(peak,process.PrivateMemorySize64);await Task.Delay(250,sampleStop.Token);}}catch(OperationCanceledException){}});
        try{
            if(attempts!=0)throw new IOException("该测试已有请求记录，禁止自动重跑。");
            var settings=JsonSerializer.Deserialize<ReadingSettings>(File.ReadAllText(configPath))!;
            if(settings.Automatic||new Uri(settings.Endpoint).Host!="api.deepseek.com")throw new IOException("真实测试必须隔离，关闭自动任务并使用官方地址。");
            using var client=new SummaryClient();models=await client.Models(settings,default);
            selected=models.Contains("deepseek-flash")?"deepseek-flash":models.FirstOrDefault(m=>m.Contains("flash",StringComparison.OrdinalIgnoreCase))??"";
            if(selected.Length==0)throw new IOException("模型列表没有可确认的Flash模型，未发送生成请求。");
            settings.Model=selected;
            var source=ReadingCatalog.All().Single(s=>s.Id=="jacs");
            var articles=ReadingContent.Parse(File.ReadAllBytes(Arg("--feed-file")),source).Where(a=>a.HasAbstract).Take(3).ToArray();
            if(articles.Length!=3)throw new IOException("没有三篇足够摘要的JACS样本。");
            foreach(var article in articles){
                if(attempts>=3)throw new IOException("已达到三次请求上限。");
                File.WriteAllText(counter,(++attempts).ToString()); // durable before dispatch
                AiUsage? usage=null;var duration=Stopwatch.StartNew();
                try{
                    var result=await client.Translate(settings,article,new[]{"ChineseTitle","ChineseAbstract","ChineseSummary"},u=>usage=u,default);
                    rows.Add(new{article.Title,article.Doi,article.Url,article.Abstract,article.PublishedDay,result,usage,seconds=duration.Elapsed.TotalSeconds});
                }catch(Exception e){rows.Add(new{article.Title,article.Doi,usage,seconds=duration.Elapsed.TotalSeconds,error=e is AiApiException?e.Message:e.GetType().Name});throw;}
            }
        }catch(Exception e){error=e is IOException?e.Message:e.GetType().Name;}
        finally{
            sampleStop.Cancel();await sampler;
            if(File.Exists(configPath))File.Delete(configPath);
            File.WriteAllText(output,JsonSerializer.Serialize(new{models,selected,attempts,error,rows,seconds=watch.Elapsed.TotalSeconds,averageWholeMachineCpuPercent=(process.TotalProcessorTime-cpu).TotalSeconds/watch.Elapsed.TotalSeconds/Environment.ProcessorCount*100,peakPrivateCommitBytes=peak,scope="isolated AI client; no WPF/audio; private commit, not private working set",temporaryConfigRemoved=!File.Exists(configPath)},new JsonSerializerOptions{WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));
        }
        return error.Length==0?0:1;
    }
}
