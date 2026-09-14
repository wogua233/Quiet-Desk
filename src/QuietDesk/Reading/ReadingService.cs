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
    private readonly ReadingStore store;private readonly HttpClient http=ReadingContent.Client();private readonly SummaryClient summaries;private readonly CancellationTokenSource stop=new();private readonly SemaphoreSlim sync=new(1),api=new(1),slots=new(2);private DateTimeOffset next=DateTimeOffset.MinValue;private readonly Task loop;private volatile bool autoDue=true;private bool limitHit;private string autoDay="";
    internal ReadingSettings Settings {get;private set;}
    internal event Action? Updated;internal string Status {get;private set;}="阅读已启用";
    internal ReadingService(string directory,ReadingSettings settings,HttpMessageHandler? summaryHandler=null,bool start=true){summaries=new(summaryHandler);Settings=Clone(settings);store=new(directory);foreach(var s in ReadingCatalog.All())if(!store.Load<Source>("source").Any(x=>x.Id==s.Id))store.Put("source",s.Id,s);store.MigrateAbstractMode();store.Prune();loop=start?Task.Run(Loop):Task.CompletedTask;}
    private static ReadingSettings Clone(ReadingSettings s)=>JsonSerializer.Deserialize<ReadingSettings>(JsonSerializer.Serialize(s))!;
    internal static ReadingSettings LoadSettings(string directory){
        ReadingSettings settings;var path=Path.Combine(directory,"reading-settings.json");
        try{settings=JsonSerializer.Deserialize<ReadingSettings>(File.ReadAllText(path))??new();}catch{settings=new();}
        if(settings.AbstractModeVersion<1){settings.DailyLimit=10;settings.AbstractModeVersion=1;if(File.Exists(path))SaveSettings(directory,settings);}
        return settings;
    }
    internal static void SaveSettings(string directory,ReadingSettings settings){Directory.CreateDirectory(directory);string path=Path.Combine(directory,"reading-settings.json");File.WriteAllText(path+".tmp",JsonSerializer.Serialize(settings));File.Move(path+".tmp",path,true);}
    internal void Configure(ReadingSettings settings){Settings=Clone(settings);autoDue=true;SaveSettings(Directory.GetParent(store.DirectoryPath)!.FullName,settings);Changed();}
    internal List<Source> Sources()=>store.Load<Source>("source");
    internal List<Article> Articles()=>store.Load<Article>("article");
    internal List<Article> Query(string day,string source,bool discovered,bool favorites,int offset,bool unread=false)=>store.Query(day,source,discovered,favorites,offset,unread);
    internal void SaveSource(Source s){store.Put("source",s.Id,s);next=DateTimeOffset.MinValue;Changed();}
    internal void RemoveSource(Source s){s.Subscribed=false;SaveSource(s);if(s.Id.StartsWith("custom-"))store.Delete("source",s.Id);}
    internal void SaveArticle(Article a){store.Put("article",a.Id,a);Changed();}
    private void SaveContent(Article a){var latest=store.Find<Article>("article",a.Id);if(latest!=null){a.Read=latest.Read;a.Favorite=latest.Favorite;}SaveArticle(a);}
    private void Changed()=>Updated?.Invoke();
    internal Task Refresh()=>Task.Run(RefreshCore);
    private async Task RefreshCore(){if(!await sync.WaitAsync(0))return;try{if(!NetworkInterface.GetIsNetworkAvailable()){Status="网络不可用，保留已有文章。";return;}var sources=Sources().Where(s=>s.Subscribed&&(!s.NextAttempt.HasValue||s.NextAttempt<=DateTimeOffset.Now)).ToList();await Task.WhenAll(sources.Select(async s=>{await slots.WaitAsync(stop.Token);try{await Fetch(s);}finally{slots.Release();}}));Status="订阅检查完成；源失败与无新文章分别显示。";autoDue=true;next=DateTimeOffset.Now.AddHours(2);store.Prune();}catch(OperationCanceledException){}catch(Exception e){Status="阅读更新失败："+e.Message;}finally{sync.Release();Changed();}}
    private async Task Fetch(Source s){try{using var req=new HttpRequestMessage(HttpMethod.Get,s.Url);if(s.ETag.Length>0)req.Headers.TryAddWithoutValidation("If-None-Match",s.ETag);if(s.Modified.Length>0)req.Headers.TryAddWithoutValidation("If-Modified-Since",s.Modified);using var response=await http.SendAsync(req,HttpCompletionOption.ResponseHeadersRead,stop.Token);if(response.StatusCode==System.Net.HttpStatusCode.NotModified){s.Status="未更新（304）";}else{var bytes=await ReadingContent.Bounded(response,4_000_000,stop.Token);var parsed=ReadingContent.Parse(bytes,s);foreach(var a in parsed){var old=store.Find<Article>("article",a.Id)??store.FindUrl(a.Url);if(old==null){if(s.LastSuccess==null&&a.PublishedDay!=null&&string.CompareOrdinal(a.PublishedDay,DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd"))<0)continue;store.Put("article",a.Id,a);}else{bool changed=old.Abstract!=a.Abstract||old.Title!=a.Title;old.Title=a.Title;old.Abstract=a.Abstract;if(a.PublishedDay!=null){old.PublishedDay=a.PublishedDay;old.DateEvidence=a.DateEvidence;}if(changed){old.SummaryKey="";old.Status="文章已更新，摘要待更新";}store.Put("article",old.Id,old);}}s.ETag=response.Headers.ETag?.ToString()??"";s.Modified=response.Content.Headers.LastModified?.ToString("R")??"";s.ArticleCount=parsed.Count;s.Status=parsed.Count==0?"更新成功 · 暂无条目":$"更新成功 · {parsed.Count}条来源记录";}s.LastSuccess=DateTimeOffset.Now;s.NextAttempt=null;s.Failures=0;}catch(Exception e)when(e is not OperationCanceledException){s.Failures++;s.NextAttempt=DateTimeOffset.Now.AddMinutes(Math.Min(120,5*Math.Pow(2,Math.Min(s.Failures,5))));s.Status="更新失败："+(e is HttpRequestException h?$"HTTP {h.StatusCode}":e.Message);}finally{var latest=store.Find<Source>("source",s.Id);if(latest!=null){s.Subscribed=latest.Subscribed;store.Put("source",s.Id,s);}Changed();}}
    internal Task LoadAbstract(Article article)=>Task.Run(async()=>{
        await slots.WaitAsync(stop.Token);
        try{var a=store.Find<Article>("article",article.Id)??article;if(a.HasAbstract||a.AbstractStatus.Length>0)return;await ArticleMetadata.Enrich(a,http,stop.Token);if(a.AbstractStatus.Length==0)a.AbstractStatus="当前来源未提供足够的公开摘要";SaveContent(a);}
        catch(OperationCanceledException){}catch(Exception e){Status="摘要补充失败："+e.Message;Changed();}finally{slots.Release();}
    });
    internal Task Summarize(Article a,bool automatic=false)=>Task.Run(()=>SummarizeCore(a,automatic));
    private async Task SummarizeCore(Article a,bool automatic){var config=Settings;if(!config.Ready||config.AbstractConsent!=config.Endpoint){Status="请在阅读设置中配置API并确认摘要发送范围。";Changed();return;}await api.WaitAsync(stop.Token);try{a=store.Find<Article>("article",a.Id)??a;if(!a.HasAbstract){await slots.WaitAsync(stop.Token);try{await ArticleMetadata.Enrich(a,http,stop.Token);}finally{slots.Release();}}if(automatic&&a.PublishedDay!=DateTime.Now.ToString("yyyy-MM-dd")){SaveContent(a);return;}string key=ReadingCatalog.Hash(a.Title+a.Abstract+config.Model+config.Endpoint+"abstract-v2");if(a.Summary.Length>0&&a.SummaryKey.Length>0)return;if(!a.HasAbstract){a.Status="资料不足，打开原文查看";SaveContent(a);return;}if(automatic&&!store.Reserve(DateTime.Now.ToString("yyyy-MM-dd"),config.DailyLimit)){Status="今日自动摘要额度已用完";limitHit=true;return;}a.Status="正在总结";SaveContent(a);string result=await summaries.Ask(config,a.Title+"\n资料范围："+a.Basis+"\n"+a.Abstract,stop.Token);a.ChineseTitle=result.Split('\n')[0].Trim('#',' ');a.Summary=result;a.SummaryKey=key;a.Status=a.Basis;SaveContent(a);}catch(Exception e)when(e is not OperationCanceledException){a.Status=e.Message;SaveContent(a);}finally{api.Release();Changed();}}
    internal async Task<string> Test(){await api.WaitAsync(stop.Token);try{return await summaries.Ask(Settings,"这是一项连接测试。请回复：连接成功。",stop.Token);}finally{api.Release();}}
    private async Task Loop(){try{while(!stop.IsCancellationRequested){if(DateTimeOffset.Now>=next){await Refresh();next=DateTimeOffset.Now.AddMinutes(NetworkInterface.GetIsNetworkAvailable()?120:1);}
            if(autoDay!=DateTime.Now.ToString("yyyy-MM-dd")){autoDay=DateTime.Now.ToString("yyyy-MM-dd");autoDue=true;}
            if(autoDue&&NetworkInterface.GetIsNetworkAvailable()&&Settings.Automatic&&Settings.Ready&&Settings.AbstractConsent==Settings.Endpoint){autoDue=false;limitHit=false;var groups=store.Pending(DateTime.Now.ToString("yyyy-MM-dd"),Settings.DailyLimit).GroupBy(a=>a.SourceId).Select(g=>new Queue<Article>(g.OrderBy(a=>a.Discovered))).ToList();while(groups.Any(g=>g.Count>0)){foreach(var group in groups.Where(g=>g.Count>0)){await Summarize(group.Dequeue(),true);if(limitHit)break;}if(limitHit)break;}}
            await Task.Delay(2000,stop.Token);}}catch(OperationCanceledException){} }
    public void Dispose(){stop.Cancel();_=loop.ContinueWith(_=>{http.Dispose();summaries.Dispose();});}
}
