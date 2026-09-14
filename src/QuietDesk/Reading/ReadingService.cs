using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
namespace QuietDesk.Reading;

internal sealed class ReadingService:IDisposable
{
    private readonly ReadingStore store;
    private readonly HttpClient http=ReadingContent.Client();
    private readonly SummaryClient summaries;
    private readonly CancellationTokenSource stop=new();
    private readonly SemaphoreSlim sync=new(1),api=new(1),slots=new(2),dispatch=new(1);
    private readonly ReadingIdleGate idle=new();
    private readonly Task loop,monitor;
    private volatile bool mayWork,immediate;
    private volatile string idleReason="等待空闲";
    private DateTimeOffset next=DateTimeOffset.MinValue;
    private AiQueueState queue;
    internal ReadingSettings Settings {get;private set;}
    internal event Action? Updated;internal event Action? QueueUpdated;
    internal string Status {get;private set;}="阅读已启用";
    internal string QueueStatus {get;private set;}="尚未开始翻译";
    internal bool QueuePaused=>queue.Paused;

    internal ReadingService(string directory,ReadingSettings settings,HttpMessageHandler? summaryHandler=null,bool start=true)
    {
        summaries=new(summaryHandler);Settings=Clone(settings);store=new(directory);
        foreach(var s in ReadingCatalog.All()){
            var existing=store.Find<Source>("source",s.Id);
            if(existing==null)store.Put("source",s.Id,s);
            else if(s.Id=="jacs"&&existing.Url=="https://pubs.acs.org/action/showFeed?type=axatoc&feed=rss&jc=jacsat"){
                existing.Url=s.Url;existing.ETag="";existing.Modified="";existing.NextAttempt=null;existing.Failures=0;existing.ParserVersion=0;existing.Status="订阅地址已更新，等待获取";store.Put("source",existing.Id,existing);
            }
        }
        store.MigrateAbstractMode();store.MigrateBilingual();store.Prune();
        queue=store.Find<AiQueueState>("ai-state","current")??new();
        // An interrupted request may already have been billed. Never automatically repeat it.
        foreach(var j in Jobs().Where(j=>j.State=="处理中")){j.State="失败";store.Put("ai-job",j.Id,j);}
        UpdateQueue();
        monitor=start?Task.Run(Monitor):Task.CompletedTask;loop=start?Task.Run(Loop):Task.CompletedTask;
    }
    private static ReadingSettings Clone(ReadingSettings s)=>JsonSerializer.Deserialize<ReadingSettings>(JsonSerializer.Serialize(s))!;
    internal static ReadingSettings LoadSettings(string directory)
    {
        try{return JsonSerializer.Deserialize<ReadingSettings>(File.ReadAllText(Path.Combine(directory,"reading-settings.json")))??new();}
        catch{return new();}
    }
    internal static void SaveSettings(string directory,ReadingSettings settings)
    {
        Directory.CreateDirectory(directory);string path=Path.Combine(directory,"reading-settings.json");
        File.WriteAllText(path+".tmp",JsonSerializer.Serialize(settings));File.Move(path+".tmp",path,true);
    }
    internal void Configure(ReadingSettings settings)
    {
        Settings=Clone(settings);SaveSettings(Directory.GetParent(store.DirectoryPath)!.FullName,settings);Changed();
    }
    internal List<Source> Sources()=>store.Load<Source>("source");
    internal List<Article> Articles()=>store.Load<Article>("article");
    internal List<Article> Query(string day,string source,bool discovered,bool favorites,int offset,bool unread=false,bool recent=false,ArticleOrder order=ArticleOrder.Newest)=>store.Query(day,source,discovered,favorites,offset,unread,recent,order);
    internal string LatestDay(string source)=>store.LatestDay(source);
    internal void SaveSource(Source s){store.Put("source",s.Id,s);next=DateTimeOffset.MinValue;Changed();}
    internal void RemoveSource(Source s){s.Subscribed=false;SaveSource(s);if(s.Id.StartsWith("custom-"))store.Delete("source",s.Id);}
    internal void SaveArticle(Article a)
    {
        // UI operations may carry an old snapshot while AI or source updates complete.
        if(store.UpdateArticle(a.Id,latest=>{latest.Read=a.Read;latest.Favorite=a.Favorite;})==null)store.Put("article",a.Id,a);
        Changed();
    }
    private void SetArticleStatus(string id,string status){store.UpdateArticle(id,a=>a.Status=status);Changed();}
    private Article? SaveMetadata(Article enriched,string fingerprint){
        var result=store.UpdateArticle(enriched.Id,current=>{
            if(ReadingAiPolicy.ContentKey(current)!=fingerprint)return;
            current.Abstract=enriched.Abstract;current.AbstractStatus=enriched.AbstractStatus;current.Basis=enriched.Basis;
        });Changed();return result;
    }
    private void Changed()=>Updated?.Invoke();
    internal Task Refresh()=>Task.Run(RefreshCore);
    private async Task RefreshCore()
    {
        await sync.WaitAsync(stop.Token);
        try{
            if(!NetworkInterface.GetIsNetworkAvailable()){Status="网络不可用，保留已有文章。";next=DateTimeOffset.Now.AddMinutes(1);return;}
            Status="正在检查订阅…";Changed();
            var sources=Sources().Where(s=>s.Subscribed&&(!s.NextAttempt.HasValue||s.NextAttempt<=DateTimeOffset.Now)).ToList();
            await Task.WhenAll(sources.Select(async s=>{await slots.WaitAsync(stop.Token);try{await Fetch(s);}finally{slots.Release();}}));
            Status="订阅检查完成";next=DateTimeOffset.Now.AddHours(2);store.Prune();
        }catch(OperationCanceledException){}catch(Exception e){Status="阅读更新失败："+e.Message;next=DateTimeOffset.Now.AddMinutes(5);}
        finally{sync.Release();Changed();}
    }
    private async Task Fetch(Source s){try{using var req=new HttpRequestMessage(HttpMethod.Get,s.Url);if(s.ParserVersion>=2&&s.ETag.Length>0)req.Headers.TryAddWithoutValidation("If-None-Match",s.ETag);if(s.ParserVersion>=2&&s.Modified.Length>0)req.Headers.TryAddWithoutValidation("If-Modified-Since",s.Modified);using var response=await http.SendAsync(req,HttpCompletionOption.ResponseHeadersRead,stop.Token);if(response.StatusCode==System.Net.HttpStatusCode.NotModified){s.Status="未更新（304）";}else{var bytes=await ReadingContent.Bounded(response,4_000_000,stop.Token);var parsed=ReadingContent.Parse(bytes,s);foreach(var a in parsed){var old=store.Find<Article>("article",a.Id)??store.FindUrl(a.Url);if(old==null){if(s.LastSuccess==null&&a.PublishedDay!=null&&string.CompareOrdinal(a.PublishedDay,DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd"))<0)continue;a.FeedHash=ReadingAiPolicy.ContentKey(a);store.Put("article",a.Id,a);}else{store.UpdateArticle(old.Id,current=>ReadingAiPolicy.MergeFeed(current,a));}}s.ETag=response.Headers.ETag?.ToString()??"";s.Modified=response.Content.Headers.LastModified?.ToString("R")??"";s.ParserVersion=2;s.ArticleCount=parsed.Count;s.Status=parsed.Count==0?"更新成功 · 暂无条目":$"更新成功 · {parsed.Count}条来源记录";}s.LastSuccess=DateTimeOffset.Now;s.NextAttempt=null;s.Failures=0;}catch(Exception e)when(!stop.IsCancellationRequested){s.Failures++;s.NextAttempt=DateTimeOffset.Now.AddMinutes(Math.Min(120,5*Math.Pow(2,Math.Min(s.Failures,5))));s.Status="更新失败："+(e is HttpRequestException h?$"HTTP {h.StatusCode}":e.Message);}finally{var latest=store.Find<Source>("source",s.Id);if(latest!=null){s.Subscribed=latest.Subscribed;store.Put("source",s.Id,s);}Changed();}}

    internal Task LoadAbstract(Article article)=>Task.Run(async()=>{
        await slots.WaitAsync(stop.Token);
        try{
            var a=store.Find<Article>("article",article.Id)??article;
            if(a.HasAbstract||a.AbstractStatus.Length>0)return;
            string fingerprint=ReadingAiPolicy.ContentKey(a);
            await ArticleMetadata.Enrich(a,http,stop.Token);
            var latest=store.Find<Article>("article",a.Id);
            if(latest!=null&&ReadingAiPolicy.ContentKey(latest)!=fingerprint)return;
            if(a.AbstractStatus.Length==0)a.AbstractStatus="当前来源未提供足够的公开摘要";
            SaveMetadata(a,fingerprint);
        }catch(OperationCanceledException){}catch(Exception e){Status="摘要补充失败："+e.Message;Changed();}finally{slots.Release();}
    });
    internal bool Complete(Article a)=>ReadingAiPolicy.Missing(a,Settings).Length==0;
    internal Task Summarize(Article a,bool automatic=false)=>Task.Run(async()=>{await Process(a.Id,automatic);});
    private async Task<bool> Process(string articleId,bool subscriptionOnly)
    {
        await api.WaitAsync(stop.Token);
        AiUsage? receipt=null;
        try{
            var config=Settings;
            if(!config.Ready||config.AbstractConsent!=config.Endpoint){Status="请配置AI服务并确认标题与摘要发送范围。";return false;}
            var a=store.Find<Article>("article",articleId);if(a==null)return false;
            if(subscriptionOnly&&(!ReadingAiPolicy.Recent(a,DateTime.Today)||!Sources().Any(s=>s.Id==a.SourceId&&s.Subscribed)))return true;
            if(ReadingAiPolicy.Missing(a,config).Length==0)return true;
            if(!a.HasAbstract&&a.AbstractStatus.Length==0){
                string before=ReadingAiPolicy.ContentKey(a);
                await slots.WaitAsync(stop.Token);try{await ArticleMetadata.Enrich(a,http,stop.Token);}finally{slots.Release();}
                a=SaveMetadata(a,before)??a;
            }
            var fields=ReadingAiPolicy.Missing(a,config);if(fields.Length==0)return true;
            if(subscriptionOnly&&!Sources().Any(s=>s.Id==a.SourceId&&s.Subscribed))return true;
            string fingerprint=ReadingAiPolicy.ContentKey(a);
            receipt=new AiUsage{ArticleId=a.Id,BatchId=queue.BatchId,Model=config.Model};
            store.Put("ai-usage",receipt.Id,receipt);
            SetArticleStatus(a.Id,"正在翻译与总结");
            var result=await summaries.Translate(config,a,fields,u=>{
                receipt.InputTokens=u.InputTokens;receipt.OutputTokens=u.OutputTokens;receipt.CachedTokens=u.CachedTokens;
                store.Put("ai-usage",receipt.Id,receipt);
            },stop.Token);
            bool applied=false;
            store.UpdateArticle(a.Id,latest=>{if(ReadingAiPolicy.ContentKey(latest)==fingerprint){ReadingAiPolicy.Apply(latest,config,result,fields);applied=true;}});
            if(!applied){receipt.State="原文已变更";store.Put("ai-usage",receipt.Id,receipt);return false;}
            receipt.State="成功";store.Put("ai-usage",receipt.Id,receipt);return true;
        }catch(OperationCanceledException)when(stop.IsCancellationRequested){if(receipt!=null){receipt.State="请求中断，结果未知";store.Put("ai-usage",receipt.Id,receipt);}throw;}
        catch(Exception e){
            string message=e is OperationCanceledException?"AI请求超时，结果未知；请检查后手动重试。":e is HttpRequestException?"AI网络连接失败，未自动重发；请检查网络或系统代理。":e is JsonException?"AI返回格式异常，旧结果已保留":e.Message;
            if(receipt!=null){receipt.State=e is OperationCanceledException?"超时，结果未知":"失败";store.Put("ai-usage",receipt.Id,receipt);}
            SetArticleStatus(articleId,message);Status=message;
            if(e is AiApiException)throw;
            return false;
        }finally{api.Release();UpdateQueue();Changed();}
    }
    internal async Task<string> Test()
    {
        await api.WaitAsync(stop.Token);
        try{
            var models=await summaries.Models(Settings,stop.Token);
            return models.Contains(Settings.Model)?"连接成功，已确认模型；未调用生成接口。":"连接成功，但未找到所填模型。可用模型："+string.Join("、",models.Take(10));
        }finally{api.Release();}
    }
    internal List<AiJob> Jobs()=>store.Load<AiJob>("ai-job");
    internal AiUsage[] Usage()=>store.Load<AiUsage>("ai-usage").ToArray();
    private void UpdateQueue()
    {
        var jobs=Jobs();var usage=store.BatchUsage(queue.BatchId);
        QueueStatus=$"{(queue.Paused?"已暂停 · ":"")}待处理 {jobs.Count(j=>j.State=="等待")} · 进行中 {jobs.Count(j=>j.State=="处理中")} · 完成 {jobs.Count(j=>j.State=="完成")} · 失败 {jobs.Count(j=>j.State=="失败")} · 本批请求 {usage.Length} · 输入 {usage.Sum(u=>u.InputTokens??0)} / 输出 {usage.Sum(u=>u.OutputTokens??0)} token · 缓存命中 {usage.Sum(u=>u.CachedTokens??0)}";
        if(usage.Any(u=>u.InputTokens==null||u.OutputTokens==null))QueueStatus+="（部分用量未知）";QueueUpdated?.Invoke();
    }
    internal Task StartNow(bool refresh=false)=>Task.Run(async()=>{
        if(refresh)await Refresh();
        await dispatch.WaitAsync(stop.Token);
        try{BuildBatch(true,DateTime.Today);}finally{dispatch.Release();}
    });
    internal void PauseQueue(){queue.Paused=true;immediate=false;store.Put("ai-state","current",queue);UpdateQueue();Changed();}
    private void BuildBatch(bool manual,DateTime today)
    {
        if(!Settings.Ready||Settings.AbstractConsent!=Settings.Endpoint){Status="请先在阅读设置中配置AI服务并确认发送范围。";Changed();return;}
        var jobs=new List<AiJob>();
        foreach(var id in store.RecentIds(today)){
            var a=store.Find<Article>("article",id);
            if(a!=null&&!Complete(a))jobs.Add(new AiJob{Id=id,ArticleId=id});
        }
        queue=new AiQueueState{Day=today.ToString("yyyy-MM-dd"),BatchId=Guid.NewGuid().ToString("N"),RetryNotBefore=queue.RetryNotBefore};
        store.SaveBatch(queue,jobs);immediate=manual;
        Status=jobs.Count==0?"近7天内容已处理完成，没有重复请求。":manual?"已开始处理近7天待完成内容。":"空闲自动批次已开始。";
        UpdateQueue();Changed();
    }
    internal async Task TickQueue(bool idleAllowed,DateTime now)
    {
        if(!await dispatch.WaitAsync(0,stop.Token))return;
        try{
            if(queue.Paused||!Settings.Ready||Settings.AbstractConsent!=Settings.Endpoint)return;
            bool allowed=Settings.Automatic&&idleAllowed&&ReadingIdleGate.TimeReady(Settings,now);
            var jobs=Jobs();
            bool pending=jobs.Any(j=>j.State=="等待");
            if(!pending&&queue.Day!=now.ToString("yyyy-MM-dd")&&allowed){BuildBatch(false,now.Date);jobs=Jobs();}
            if(!immediate&&!allowed)return;
            if(queue.RetryNotBefore>DateTimeOffset.Now)return;
            if(!NetworkInterface.GetIsNetworkAvailable()){Status="断网，队列等待恢复";return;}
            var job=jobs.FirstOrDefault(j=>j.State=="等待"&&(!j.NextAttempt.HasValue||j.NextAttempt<=DateTimeOffset.Now));
            if(job==null){if(!jobs.Any(j=>j.State=="等待")){immediate=false;QueueUpdated?.Invoke();}return;}
            var article=store.Find<Article>("article",job.ArticleId);
            if(article==null||!ReadingAiPolicy.Recent(article,now.Date)||!Sources().Any(s=>s.Id==article.SourceId&&s.Subscribed)){job.State="跳过";store.Put("ai-job",job.Id,job);UpdateQueue();Changed();return;}
            job.State="处理中";job.Attempts++;store.Put("ai-job",job.Id,job);UpdateQueue();Changed();
            try{job.State=await Process(job.ArticleId,true)?"完成":"失败";}
            catch(AiApiException e){
                // Only explicit server refusals are automatically retried; uncertain transport failures are not.
                if(e.StatusCode==429||e.StatusCode==503){
                    job.State=job.Attempts<3?"等待":"失败";job.NextAttempt=DateTimeOffset.Now.AddSeconds(Math.Clamp(e.RetryDelay.TotalSeconds*Math.Pow(2,job.Attempts-1),60,600));
                    queue.RetryNotBefore=job.NextAttempt;store.Put("ai-state","current",queue);
                }else{job.State="失败";if(e.StatusCode is 401 or 402 or 403){queue.Paused=true;store.Put("ai-state","current",queue);}}
            }
            catch(OperationCanceledException){job.State="失败";throw;}
            finally{store.Put("ai-job",job.Id,job);UpdateQueue();Changed();}
        }finally{dispatch.Release();}
    }
    private async Task Monitor()
    {
        try{while(!stop.IsCancellationRequested){
            string before=idleReason;if(Settings.Automatic){mayWork=idle.Allowed();idleReason=idle.Reason;}else mayWork=false;if(before!=idleReason)QueueUpdated?.Invoke();
            await Task.Delay(2000,stop.Token);
        }}catch(OperationCanceledException){}
    }
    private async Task Loop()
    {
        try{while(!stop.IsCancellationRequested){
            try{
                if(DateTimeOffset.Now>=next)await Refresh();
                await TickQueue(mayWork&&ReadingIdleGate.InputIsIdle(),DateTime.Now);
            }catch(OperationCanceledException){throw;}catch(Exception e){Status="阅读任务暂缓："+e.Message;Changed();}
            await Task.Delay(2000,stop.Token);
        }}catch(OperationCanceledException){}
    }
    internal string ScheduleStatus=>queue.Paused?"队列已暂停":queue.RetryNotBefore>DateTimeOffset.Now?"服务限流，等待至 "+queue.RetryNotBefore.Value.ToLocalTime().ToString("HH:mm:ss"):immediate?"手动处理模式":!Settings.Automatic?"自动处理未启用":!ReadingIdleGate.TimeReady(Settings,DateTime.Now)?"等待指定时刻："+Settings.ScheduledTime:queue.Day==DateTime.Today.ToString("yyyy-MM-dd")&&!Jobs().Any(j=>j.State=="等待")?"今日批次已结束":idleReason;
    public void Dispose()
    {
        stop.Cancel();
        _=Task.WhenAll(loop,monitor).ContinueWith(_=>{http.Dispose();summaries.Dispose();});
    }
}
