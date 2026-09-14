using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
namespace QuietDesk.Reading;

internal static class AiVerification
{
    internal sealed class Handler:HttpMessageHandler
    {
        internal int Calls;
        internal string Last="";
        internal int Code=200;internal int DelayMs;
        internal bool Malformed,Truncated,Timeout;
        internal TaskCompletionSource? Hold;
        internal TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            if(request.Method==HttpMethod.Get)return new(HttpStatusCode.OK){Content=new StringContent("{\"data\":[{\"id\":\"deepseek-flash\"}]}")};
            if(Timeout)throw new TaskCanceledException("fixture timeout");
            Calls++;Last=await request.Content!.ReadAsStringAsync(token);Entered.TrySetResult();
            if(DelayMs>0)await Task.Delay(DelayMs,token);
            if(Hold!=null)await Hold.Task.WaitAsync(token);
            return new((HttpStatusCode)Code){Content=new StringContent(JsonSerializer.Serialize(new{
                choices=new[]{new{finish_reason=Truncated?"length":"stop",message=new{content=Malformed?"not json":JsonSerializer.Serialize(new{ChineseTitle="电解质润滑剂",ChineseAbstract="中文摘要译文保留了原文结果及限定条件。",ChineseSummary="研究提出并验证了材料界面改进方法。"})}}},
                usage=new{prompt_tokens=120,completion_tokens=80,prompt_cache_hit_tokens=64}
            }))};
        }
    }
    private static ReadingSettings Settings()=>new(){Endpoint="https://api.deepseek.com",Model="deepseek-flash",AbstractConsent="https://api.deepseek.com",Automatic=true};
    private static Article Entry(string id)=>new(){Id=id,Title="Electrolyte study "+id,Abstract=new string('A',180),SourceId="jacs",PublishedDay=DateTime.Today.ToString("yyyy-MM-dd")};
    internal static async Task Checks(string dir,List<string> lines)
    {
        void Check(bool condition,string name){if(!condition)throw new Exception(name);lines.Add("PASS "+name);}
        var config=Settings();config.SetKey("fixture-key-not-real");Directory.CreateDirectory(dir);
        var h=new Handler();using var service=new ReadingService(dir,config,h,false);var db=new ReadingStore(dir);
        Check(service.Sources().Count(s=>s.Subscribed)==1&&service.Sources().Single(s=>s.Subscribed).Id=="jacs","bilingual migration defaults existing subscriptions to JACS only");
        var a=Entry("one");service.SaveArticle(a);
        await Task.WhenAll(service.Summarize(a),service.Summarize(a));
        Check(h.Calls==1,"one combined request and concurrent click deduplication");
        var saved=db.Find<Article>("article",a.Id)!;
        Check(saved.ChineseTitle.Length>0&&saved.ChineseAbstract.Length>0&&saved.Summary.Length>0&&service.Complete(saved),"three Chinese fields saved independently");
        using(var request=JsonDocument.Parse(h.Last))Check(request.RootElement.GetProperty("thinking").GetProperty("type").GetString()=="disabled"&&!h.Last.Contains("fixture-key-not-real")&&!h.Last.Contains("\"tools\""),"non-thinking JSON request excludes tools and secrets from body");
        var usage=service.Usage().Single();Check(usage.InputTokens==120&&usage.OutputTokens==80&&usage.CachedTokens==64,"reported input output and cache-hit usage retained");
        await service.Summarize(saved);Check(h.Calls==1,"opening completed bilingual cache uses no API");
        var old=Entry("legacy");old.ChineseTitle="旧标题";old.Summary="旧总结";old.SummaryKey="old";service.SaveArticle(old);
        await service.Summarize(old);
        using(var body=JsonDocument.Parse(h.Last)){
            using var material=JsonDocument.Parse(body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
            Check(material.RootElement.GetProperty("requested_fields").EnumerateArray().Select(x=>x.GetString()).SequenceEqual(new[]{"ChineseAbstract"}),"legacy summary requests only missing translation");
        }
        Check(db.Find<Article>("article",old.Id)!.Summary=="旧总结","legacy summary retained after translation");
        var snippet=Entry("title-only");snippet.Abstract="short";snippet.AbstractStatus="fixture";service.SaveArticle(snippet);await service.Summarize(snippet);
        var titleOnly=db.Find<Article>("article",snippet.Id)!;
        Check(titleOnly.ChineseTitle.Length>0&&titleOnly.ChineseAbstract.Length==0&&titleOnly.Summary.Length==0&&titleOnly.Status.Contains("不足"),"insufficient abstract produces title translation only");
        var incoming=Entry("one");saved.FeedHash=ReadingAiPolicy.ContentKey(incoming);saved.Abstract="Supplemented "+new string('B',200);string text=saved.Abstract,key=saved.AiContentKey;
        ReadingAiPolicy.MergeFeed(saved,incoming);Check(saved.Abstract==text&&saved.AiContentKey==key,"unchanged feed does not destroy supplemented abstract or cache");
        incoming.Abstract="Revised "+new string('C',180);ReadingAiPolicy.MergeFeed(saved,incoming);
        Check(saved.Abstract==incoming.Abstract&&saved.ChineseAbstract.Length>0&&ReadingAiPolicy.Missing(saved,config).Length==3,"content revision retains old display and invalidates all affected results");
        var modelChanged=Settings();modelChanged.Model="different";Check(ReadingAiPolicy.Missing(db.Find<Article>("article","one")!,modelChanged).Length==3,"cache identity includes model");
        foreach(var body in new[]{"","not json","{\"ChineseTitle\":\"\"}"}){
            bool rejected=false;try{SummaryClient.Parse(body,new[]{"ChineseTitle"});}catch(IOException){rejected=true;}Check(rejected,"invalid AI response cannot be marked complete");
        }
        var broken=Entry("broken");service.SaveArticle(broken);h.Truncated=true;await service.Summarize(broken);h.Truncated=false;
        Check(db.Find<Article>("article","broken")!.AiContentKey.Length==0,"truncated output retains incomplete state");
        h.Malformed=true;await service.Summarize(broken);h.Malformed=false;
        Check(db.Find<Article>("article","broken")!.AiContentKey.Length==0,"malformed API response is persisted as incomplete");
        h.Timeout=true;await service.Summarize(broken);h.Timeout=false;
        Check(db.Find<Article>("article","broken")!.Status.Contains("超时"),"cloud timeout does not cancel the reading scheduler or mark result complete");
        var unknown=Entry("unknown-date");unknown.PublishedDay=null;unknown.Discovered=DateTimeOffset.Now;service.SaveArticle(unknown);
        var outside=Entry("outside-week");outside.PublishedDay=DateTime.Today.AddDays(-7).ToString("yyyy-MM-dd");service.SaveArticle(outside);
        Check(db.RecentIds(DateTime.Today).Contains(unknown.Id)&&!db.RecentIds(DateTime.Today).Contains(outside.Id),"automatic scope uses discovery day when unknown and excludes eighth day");
        Check(ReadingIdleGate.CanRun(180000,30,false,false)&&!ReadingIdleGate.CanRun(179999,30,false,false)&&!ReadingIdleGate.CanRun(180000,29,false,false)&&!ReadingIdleGate.CanRun(180000,30,true,false)&&!ReadingIdleGate.CanRun(180000,30,false,true),"idle CPU battery saver and presentation gates");
        var schedule=Settings();schedule.Scheduled=true;
        Check(!ReadingIdleGate.TimeReady(schedule,DateTime.Today.AddHours(20)),"scheduled time never invented when empty");
        schedule.ScheduledTime="20:30";Check(!ReadingIdleGate.TimeReady(schedule,DateTime.Today.AddHours(20))&&ReadingIdleGate.TimeReady(schedule,DateTime.Today.AddHours(21)),"specified time is earliest eligible time");
        await QueueChecks(Path.Combine(dir,"queue"),lines);
    }
    internal static async Task<int> ShortCheck(string[] args){
        string dir=Path.Combine(Path.GetTempPath(),"QuietDesk-ai-short-"+Guid.NewGuid());var settings=Settings();settings.SetKey("fixture");
        using(var seed=new ReadingService(dir,settings,new Handler(),false)){foreach(var source in seed.Sources()){source.NextAttempt=DateTimeOffset.Now.AddDays(1);seed.SaveSource(source);}seed.PauseQueue();}
        var handler=new Handler{DelayMs=3000};using var service=new ReadingService(dir,settings,handler);
        service.SaveArticle(Entry("perf"));using var process=System.Diagnostics.Process.GetCurrentProcess();
        await Task.Delay(2000);var measurements=new List<object>();
        async Task Measure(string phase,Task work){
            process.Refresh();long initial=process.PrivateMemorySize64,peak=initial;var cpu=process.TotalProcessorTime;var watch=System.Diagnostics.Stopwatch.StartNew();
            for(int i=0;i<60;i++){await Task.Delay(250);process.Refresh();peak=Math.Max(peak,process.PrivateMemorySize64);}
            await work;measurements.Add(new{phase,seconds=watch.Elapsed.TotalSeconds,averageWholeMachineCpuPercent=(process.TotalProcessorTime-cpu).TotalSeconds/watch.Elapsed.TotalSeconds/Environment.ProcessorCount*100,initialPrivateCommitBytes=initial,peakPrivateCommitBytes=peak,incrementBytes=peak-initial});
        }
        await Measure("paused queue with idle monitor",Task.CompletedTask);
        await Measure("single simulated cloud request and saved result",service.Summarize(Entry("perf")));
        File.WriteAllText(args[Array.IndexOf(args,"--out")+1],JsonSerializer.Serialize(new{measurements,simulatedRequests=handler.Calls,scope="isolated reading service; mock API; private commit is not private working set; no audio/WPF"},new JsonSerializerOptions{WriteIndented=true}));return 0;
    }
    private static async Task QueueChecks(string dir,List<string> lines)
    {
        void Check(bool condition,string name){if(!condition)throw new Exception(name);lines.Add("PASS "+name);}
        var c=Settings();c.SetKey("fixture");var h=new Handler();
        using(var service=new ReadingService(dir,c,h,false)){
            service.SaveArticle(Entry("a"));service.SaveArticle(Entry("b"));
            await service.TickQueue(false,DateTime.Now);Check(h.Calls==0,"automatic batch waits for idle");
            await service.TickQueue(true,DateTime.Now);Check(h.Calls==1,"idle starts daily batch");
            service.PauseQueue();await service.TickQueue(true,DateTime.Now);Check(h.Calls==1,"pause prevents next dispatch");
        }
        using(var service=new ReadingService(dir,c,h,false)){
            Check(service.QueuePaused,"queue pause survives restart");
            await service.StartNow();await service.TickQueue(false,DateTime.Now);
            Check(h.Calls==2,"manual start bypasses idle and resumes only incomplete work");
            service.SaveArticle(Entry("late"));await service.TickQueue(true,DateTime.Now);Check(h.Calls==2,"late items wait after daily snapshot finishes");
            await service.StartNow();var jacs=service.Sources().Single(s=>s.Id=="jacs");jacs.Subscribed=false;service.SaveSource(jacs);await service.TickQueue(false,DateTime.Now);
            Check(h.Calls==2,"unsubscribe is checked before dispatch");
            jacs.Subscribed=true;service.SaveSource(jacs);await service.StartNow();
            h.Hold=new(TaskCreationOptions.RunContinuationsAsynchronously);h.Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
            var running=service.TickQueue(false,DateTime.Now);await h.Entered.Task;
            service.PauseQueue();h.Hold.SetResult();await running;h.Hold=null;
            Check(service.QueuePaused&&service.Jobs().All(j=>j.State!="处理中"),"pause allows in-flight cloud result to finish");
            service.SaveArticle(Entry("limited"));service.SaveArticle(Entry("other-limited"));await service.StartNow();h.Code=429;await service.TickQueue(false,DateTime.Now);
            var retry=service.Jobs().Single(j=>j.ArticleId=="limited");int calls=h.Calls;await service.TickQueue(false,DateTime.Now);
            Check(retry.State=="等待"&&retry.NextAttempt>DateTimeOffset.Now&&h.Calls==calls,"global rate limit backoff prevents dispatching another queued article");
            h.Code=200;
            var db=new ReadingStore(dir);var state=db.Find<AiQueueState>("ai-state","current")!;
            state.RetryNotBefore=null;db.Put("ai-state","current",state);
        }
        using(var service=new ReadingService(dir,c,h,false)){
            await service.StartNow();while(service.Jobs().Any(j=>j.State=="等待"))await service.TickQueue(false,DateTime.Now);
            int before=h.Calls;service.SaveArticle(Entry("tomorrow"));await service.TickQueue(true,DateTime.Now.AddDays(1));
            Check(h.Calls==before+1,"next local day starts a new batch without repeating completed work");
            var other=service.Sources().Single(s=>s.Id=="prl");other.Subscribed=true;service.SaveSource(other);
        }
        using(var service=new ReadingService(dir,c,h,false)){
            Check(service.Sources().Single(s=>s.Id=="prl").Subscribed,"JACS-only migration does not repeat after user subscription changes");
        }
    }
}
