using System;
using System.IO;
using System.Linq;
using System.Text;
namespace QuietDesk.Reading;
internal static class ReadingVerification
{
    internal static int Run(string[] args){if(args.Contains("--short-check"))return ShortCheck(args).GetAwaiter().GetResult();if(args.Contains("--layout"))return Layout(args);if(args.Contains("--live"))return Live(args).GetAwaiter().GetResult();int oi=Array.IndexOf(args,"--out");string output=oi<0?"reading-test.txt":args[oi+1];var lines=new System.Collections.Generic.List<string>();void Check(bool ok,string name){if(!ok)throw new Exception(name);lines.Add("PASS "+name);}try{
        var source=ReadingCatalog.All()[0];string xml="<rss><channel><item><title>Study</title><link>https://example.org/10.1234/test?utm_source=x</link><pubDate>2026-09-12</pubDate><description><![CDATA[<p>Abstract</p><script>bad</script>]]></description></item></channel></rss>";var items=ReadingContent.Parse(Encoding.UTF8.GetBytes(xml),source);Check(items.Count==1&&items[0].PublishedDay=="2026-09-12"&&items[0].Abstract=="Abstract","RSS dates, HTML cleaning and parsing");Check(items[0].Doi=="10.1234/test"&&!items[0].Url.Contains("utm_"),"DOI identity and tracking normalization");
        try{ReadingContent.Parse(Encoding.UTF8.GetBytes("<!DOCTYPE rss [<!ENTITY x SYSTEM 'file:///c:/windows/win.ini'>]><rss>&x;</rss>"),source);throw new Exception("DTD allowed");}catch(System.Xml.XmlException){lines.Add("PASS external XML entities rejected");}
        Check(ReadingContent.Parse(Encoding.UTF8.GetBytes("<!DOCTYPE rss SYSTEM 'https://127.0.0.1:1/not-fetched.dtd'>"+xml),source).Count==1,"feed DOCTYPE ignored without fetching an external DTD");
        try{ReadingContent.Parse(Encoding.UTF8.GetBytes("<!DOCTYPE html><html><head><title>Verify</title></head><body><br>Not XML & unknown entity"),source);throw new Exception("HTML challenge accepted");}catch(IOException e){Check(e.Message.Contains("来源返回了网页"),"HTML challenge with DOCTYPE diagnosed before parsing malformed HTML body");}
        try{ReadingContent.Parse(Encoding.UTF8.GetBytes("<!DOCTYPE rss [<!ENTITY x 'invented abstract'>]><rss><channel><item><title>&x;</title></item></channel></rss>"),source);throw new Exception("internal entity expanded");}catch(System.Xml.XmlException){lines.Add("PASS internal DTD entities are not expanded");}
        var settings=new ReadingSettings();settings.SetKey("fixture-key-not-real");Check(!System.Text.Json.JsonSerializer.Serialize(settings).Contains("fixture-key-not-real")&&settings.Key=="fixture-key-not-real"&&!settings.ProtectedKey.Contains("fixture"),"current-user secret protection");
        string dir=Path.Combine(Path.GetTempPath(),"QuietDesk-reading-test-"+Guid.NewGuid());var store=new ReadingStore(dir);store.Put("article",items[0].Id,items[0]);store.Put("article",items[0].Id,items[0]);Check(store.Load<Article>("article").Count==1,"SQLite duplicate identity upsert");Check(Enumerable.Range(0,10).All(_=>store.Reserve("2026-09-12",10))&&!store.Reserve("2026-09-12",10)&&new ReadingStore(dir).Reserve("2026-09-13",10),"persisted daily quota and day rollover");
        var vm=new ReadingViewModel(Path.Combine(dir,"disabled"));Check(vm.Service==null&&!Directory.Exists(Path.Combine(dir,"disabled")),"disabled module creates no database or polling");vm.Dispose();
        var apiSettings=new ReadingSettings{Endpoint="https://fixture.invalid/v1",Model="fixture"};apiSettings.SetKey("fixture-key-not-real");var handler=new FixtureHandler();using(var api=new SummaryClient(handler)){Check(api.Ask(apiSettings,"A sufficiently long abstract with scientific evidence.",default).GetAwaiter().GetResult()=="测试标题\n测试摘要","compatible API response parsing");Check(handler.Body.Contains("max_tokens")&&!handler.Body.Contains("fixture-key-not-real")&&!handler.Body.Contains("\"tools\""),"API payload excludes secrets and tool execution");handler.Fail=true;try{api.Ask(apiSettings,"fixture",default).GetAwaiter().GetResult();throw new Exception("rate limit ignored");}catch(IOException e){Check(e.Message.Contains("限流"),"rate limit is explicit and not retried");}}
        var unknown=ReadingContent.Parse(Encoding.UTF8.GetBytes("<feed xmlns='http://www.w3.org/2005/Atom'><entry><title>Atom item</title><link href='https://example.org/atom'/><updated>2026-09-12T12:00:00Z</updated></entry></feed>"),source).Single();Check(unknown.PublishedDay==null,"Atom update time is not misreported as publication time");
        var oldArticle=new Article{Id="old",Discovered=DateTimeOffset.Now.AddDays(-100)};var favorite=new Article{Id="favorite",Discovered=oldArticle.Discovered,Favorite=true};store.Put("article",oldArticle.Id,oldArticle);store.Put("article",favorite.Id,favorite);store.Prune();Check(store.Find<Article>("article","old")==null&&store.Find<Article>("article","favorite")!=null,"retention removes expired ordinary items but preserves favorites");
        Check(store.Query("2026-09-12","",false,false,0).Count==1,"database date filtering and paging");

        var legacy=new ReadingSettings{DailyLimit=50,Automatic=true,Endpoint="https://fixture.invalid/v1",Model="fixture",ProtectedKey=settings.ProtectedKey};
        string migration=Path.Combine(dir,"migration");ReadingService.SaveSettings(migration,legacy);
        var migrated=ReadingService.LoadSettings(migration);
        Check(migrated.DailyLimit==50&&migrated.Automatic&&migrated.ProtectedKey==legacy.ProtectedKey,"legacy settings retain encrypted key and automatic choice without imposing an AI quota");
        migrated.DailyLimit=17;ReadingService.SaveSettings(migration,migrated);
        Check(ReadingService.LoadSettings(migration).DailyLimit==17,"later user quota survives restart");
        store.Put("job","legacy",new{File=Path.Combine(dir,"original.pdf")});File.WriteAllText(Path.Combine(dir,"original.pdf"),"user original");
        Directory.CreateDirectory(Path.Combine(dir,"cache"));File.WriteAllText(Path.Combine(dir,"cache","old.output"),"temporary");
        items[0].Summary="旧摘要总结";items[0].SummaryKey="old-v1";items[0].Favorite=true;store.Put("article",items[0].Id,items[0]);
        store.MigrateAbstractMode();
        Check(store.Load<object>("job").Count==0&&!File.Exists(Path.Combine(dir,"cache","old.output"))&&File.Exists(Path.Combine(dir,"original.pdf")),"legacy tasks and owned cache removed without touching original PDF");
        Check(store.Find<Article>("article",items[0].Id)!.Summary=="旧摘要总结"&&store.Find<Article>("article",items[0].Id)!.Favorite,"migration preserves summaries and favorites");
        Check(ReadingContent.Parse(Encoding.UTF8.GetBytes("<rss><channel/></rss>"),source).Count==0,"valid empty feed is an empty result");
        try{ReadingContent.Parse(Encoding.UTF8.GetBytes("<html>Challenge</html>"),source);throw new Exception("challenge accepted");}catch(IOException){lines.Add("PASS challenge HTML is a source failure");}
        var contentOnly=ReadingContent.Parse(Encoding.UTF8.GetBytes("<feed><entry><title>Title</title><link href='https://example.org/full'/><content>FULL BODY MUST NOT BE USED</content></entry></feed>"),source).Single();
        Check(contentOnly.Abstract.Length==0,"feed full content is not treated as abstract");
        Check(store.Query("2026-09-12","",false,false,0,true).Count==1,"unread filter evaluated before pagination");
        Check(typeof(ReadingService).Assembly.GetType("QuietDesk.Reading.ReadingWorker")==null,"no full-text worker in release assembly");

        var serialHandler=new FixtureHandler();apiSettings.AbstractConsent=apiSettings.Endpoint;apiSettings.DailyLimit=10;
        string serviceDir=Path.Combine(dir,"service");var serviceStore=new ReadingStore(serviceDir);
        using(var service=new ReadingService(serviceDir,apiSettings,serialHandler,false)){
            var article=new Article{Id="serial",Title="Research title",Abstract=new string('A',200),PublishedDay=DateTime.Today.ToString("yyyy-MM-dd")};
            service.SaveArticle(article);
            System.Threading.Tasks.Task.WhenAll(service.Summarize(article),service.Summarize(article)).GetAwaiter().GetResult();
            Check(serialHandler.Calls==1,"simultaneous manual generation is deduplicated after serial gate");
            var cached=serviceStore.Find<Article>("article","serial")!;cached.SummaryKey="legacy-v1";service.SaveArticle(cached);
            service.Summarize(cached).GetAwaiter().GetResult();Check(serialHandler.Calls==1,"old summary cache is reused without regeneration");
            for(int i=0;i<12;i++){var entry=new Article{Id="auto"+i,SourceId="jacs",Title="研究 "+i,Abstract=new string('B',200),PublishedDay=DateTime.Today.ToString("yyyy-MM-dd")};service.SaveArticle(entry);service.Summarize(entry,true).GetAwaiter().GetResult();}
            Check(serialHandler.Calls==13,"service has no ten-article generation cap");
        }

        var snippet=ReadingContent.Parse(Encoding.UTF8.GetBytes("<rss><channel><item><title>Truncated</title><link>https://example.org/snippet</link><description><![CDATA[Author(s): Person<br/><p>"+new string('A',150)+"…</p><br/>Volume 1 Published today]]></description></item></channel></rss>"),source).Single();
        Check(!snippet.HasAbstract&&!snippet.Abstract.Contains("Author(s)")&&!snippet.Abstract.Contains("Volume"),"APS authors removed and truncated excerpt cannot trigger summary");
        var rangeStore=new ReadingStore(Path.Combine(dir,"range"));
        foreach(var entry in new[]{new Article{Id="friday",SourceId="prl",PublishedDay="2026-09-11"},new Article{Id="boundary",SourceId="prl",PublishedDay="2026-09-08"},new Article{Id="outside",SourceId="prl",PublishedDay="2026-09-07"},new Article{Id="unknown",SourceId="prl",Discovered=DateTimeOffset.Parse("2026-09-14T08:00:00+08:00")},new Article{Id="other",SourceId="bbc",PublishedDay="2026-09-14"}})rangeStore.Put("article",entry.Id,entry);
        Check(rangeStore.Query("2026-09-14","prl",false,false,0).Count==0&&rangeStore.Query("2026-09-14","prl",false,false,0,false,true).Count==3,"recent week shows Friday journal on Monday, respects boundary and includes unknown-date discovery");
        Check(rangeStore.LatestDay("prl")=="2026-09-11","empty date reports latest stored publication");
        var nature=ReadingCatalog.All().Single(s=>s.Id=="nchem");
        var natureArticle=ReadingContent.Parse(Encoding.UTF8.GetBytes("<rss xmlns:content='http://purl.org/rss/1.0/modules/content/'><channel><item><title>Study</title><link>https://www.nature.com/articles/test</link><content:encoded><![CDATA[<p>Nature Chemistry, Published online: date; doi:example</p>A scientific standfirst with a finding and its significance.]]></content:encoded></item></channel></rss>"),nature).Single();
        Check(natureArticle.Abstract.StartsWith("A scientific")&&!natureArticle.Abstract.Contains("Published online:")&&natureArticle.Basis=="基于导读","official Nature encoded standfirst excludes metadata header");
        Check(new ReadingViewModel(Path.Combine(dir,"default-view")).Recent,"view defaults to recent week");
        var migratedJacs=new Source{Id="jacs",Name="JACS",Url="https://pubs.acs.org/action/showFeed?type=axatoc&feed=rss&jc=jacsat",Subscribed=true,Failures=4,NextAttempt=DateTimeOffset.Now.AddHours(2),ETag="old",Modified="old"};
        var jacsDirectory=Path.Combine(dir,"jacs-migration");var jacsStore=new ReadingStore(jacsDirectory);jacsStore.Put("source","jacs",migratedJacs);
        using(var migratedService=new ReadingService(jacsDirectory,new ReadingSettings(),start:false)){
            var actual=migratedService.Sources().Single(s=>s.Id=="jacs");
            Check(actual.Subscribed&&actual.Url=="https://pubs.acs.org/rss/jacsat/asap.xml"&&actual.ETag==""&&actual.Modified==""&&actual.NextAttempt==null&&actual.Failures==0,"JACS migration preserves subscription and clears stale request/backoff state");
        }
        migratedJacs.Url="https://example.org/custom";jacsStore.Put("source","jacs",migratedJacs);
        using(var customService=new ReadingService(jacsDirectory,new ReadingSettings(),start:false))Check(customService.Sources().Single(s=>s.Id=="jacs").Url==migratedJacs.Url,"JACS migration does not overwrite a customized URL");
        int fixture=Array.IndexOf(args,"--feed-file");
        int natureFixture=Array.IndexOf(args,"--nature-feed-file");
        if(natureFixture>=0){var articles=ReadingContent.Parse(File.ReadAllBytes(args[natureFixture+1]),nature);Check(articles.Count>0&&articles.All(a=>a.SourceId=="nchem"&&a.Url.StartsWith("https://www.nature.com/")),"real Nature Chemistry feed parses titles and article links");lines.Add("INFO Nature Chemistry actual feed entries: "+articles.Count);}
        if(fixture>=0){var articles=ReadingContent.Parse(File.ReadAllBytes(args[fixture+1]),ReadingCatalog.All().Single(s=>s.Id=="jacs"));Check(articles.Count>0&&articles.All(a=>a.Doi.StartsWith("10.1021/")&&!a.Doi.Contains("https:"))&&articles.Any(a=>a.HasAbstract),"real JACS feed parses DOI, titles and usable abstracts");}
        var ordering=new ReadingStore(Path.Combine(dir,"order"));
        ordering.Put("source","a",new Source{Id="a",Subscribed=true});ordering.Put("source","b",new Source{Id="b",Subscribed=false});
        foreach(var a in new[]{new Article{Id="new",SourceId="a",SourceName="Z Journal",PublishedDay="2026-09-14",Discovered=DateTimeOffset.Parse("2026-09-14T00:00:00Z")},new Article{Id="old",SourceId="a",SourceName="A Journal",PublishedDay="2026-09-10",Discovered=DateTimeOffset.Parse("2026-09-15T00:00:00Z")},new Article{Id="hidden",SourceId="b",SourceName="B Journal",PublishedDay="2026-09-13"}})ordering.Put("article",a.Id,a);
        Check(ordering.Query("2026-09-14",ReadingCatalog.SubscribedFilter,false,false,0,false,true).Select(a=>a.Id).SequenceEqual(new[]{"new","old"}),"subscribed filter excludes unsubscribed cached articles and defaults to newest publication");
        Check(ordering.Query("2026-09-14","",false,false,0,false,true).Count==3,"all publications remains explicit opt-in");
        Check(ordering.Query("2026-09-14",ReadingCatalog.SubscribedFilter,false,false,0,false,true,ArticleOrder.Oldest).Select(a=>a.Id).SequenceEqual(new[]{"old","new"}),"oldest publication first ignores later discovery order");
        Check(ordering.Query("2026-09-14","",false,false,0,false,true,ArticleOrder.Journal).Select(a=>a.SourceName).SequenceEqual(new[]{"A Journal","B Journal","Z Journal"}),"journal name sorting precedes time ordering");
        Check(ordering.LatestDay(ReadingCatalog.SubscribedFilter)=="2026-09-14","latest date respects subscribed scope");
        ordering.Put("source","a",new Source{Id="a",Subscribed=false});Check(ordering.Query("2026-09-14",ReadingCatalog.SubscribedFilter,false,false,0,false,true).Count==0,"unsubscribing immediately removes items from subscribed view");
        using(var defaults=new ReadingViewModel(Path.Combine(dir,"filter-defaults")))Check(defaults.SourceId==ReadingCatalog.SubscribedFilter&&defaults.NewestFirst&&!defaults.SortByJournal,"view defaults to subscribed publications and newest first");
        SourceVerification.Checks(Path.Combine(dir,"source-tests"),lines).GetAwaiter().GetResult();
        AiVerification.Checks(Path.Combine(dir,"ai-tests"),lines).GetAwaiter().GetResult();
        File.WriteAllLines(output,lines);return 0;
    }catch(Exception e){lines.Add("FAIL "+e);File.WriteAllLines(output,lines);return 1;}}

    private static async System.Threading.Tasks.Task<int> Live(string[] args){int oi=Array.IndexOf(args,"--out");string output=args[oi+1];var rows=new System.Collections.Generic.List<object>();using var client=ReadingContent.Client();foreach(var source in ReadingCatalog.All()){try{using var response=await client.GetAsync(source.Url,System.Net.Http.HttpCompletionOption.ResponseHeadersRead);var bytes=await ReadingContent.Bounded(response,4000000,default);var articles=ReadingContent.Parse(bytes,source);rows.Add(new{source.Name,source.Url,status=(int)response.StatusCode,count=articles.Count,result=articles.Count>0?"parsed":"empty",sample=articles.FirstOrDefault()?.Title,abstractCharacters=articles.FirstOrDefault()?.Abstract.Length,latestDay=articles.Max(a=>a.PublishedDay),todayCount=articles.Count(a=>a.PublishedDay==DateTime.Today.ToString("yyyy-MM-dd")),recentCount=articles.Count(a=>a.PublishedDay!=null&&string.CompareOrdinal(a.PublishedDay,DateTime.Today.AddDays(-6).ToString("yyyy-MM-dd"))>=0&&string.CompareOrdinal(a.PublishedDay,DateTime.Today.ToString("yyyy-MM-dd"))<=0)});}catch(Exception e){rows.Add(new{source.Name,source.Url,result="failed",error=e.Message});}File.WriteAllText(output,System.Text.Json.JsonSerializer.Serialize(rows,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));}return 0;}

    private static async System.Threading.Tasks.Task<int> ShortCheck(string[] args){
        string output=args[Array.IndexOf(args,"--out")+1],dir=Path.Combine(Path.GetTempPath(),"QuietDesk-short-"+Guid.NewGuid());
        var store=new ReadingStore(dir);
        using(var db=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+Path.Combine(dir,"reading.db")+";Pooling=False")){
            db.Open();using var tx=db.BeginTransaction();using var cmd=db.CreateCommand();cmd.Transaction=tx;cmd.CommandText="INSERT INTO objects VALUES('article',$id,$json)";cmd.Parameters.AddWithValue("$id","");cmd.Parameters.AddWithValue("$json","");
            for(int i=0;i<10000;i++){cmd.Parameters["$id"].Value=i.ToString();cmd.Parameters["$json"].Value=System.Text.Json.JsonSerializer.Serialize(new Article{Id=i.ToString(),SourceId="bbc",Title="Cached entry "+i,PublishedDay=DateTime.Today.ToString("yyyy-MM-dd")});cmd.ExecuteNonQuery();}tx.Commit();}
        using var service=new ReadingService(dir,new ReadingSettings());var source=service.Sources().Single(s=>s.Id=="bbc");source.Subscribed=true;
        var watch=System.Diagnostics.Stopwatch.StartNew();service.SaveSource(source);await service.Refresh();
        while(service.Sources().Single(s=>s.Id=="bbc").LastSuccess==null&&watch.Elapsed.TotalSeconds<45){await System.Threading.Tasks.Task.Delay(100);if(service.Sources().Single(s=>s.Id=="bbc").Failures>0)break;}
        double syncSeconds=watch.Elapsed.TotalSeconds;
        var query=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<100;i++)service.Query(DateTime.Today.ToString("yyyy-MM-dd"),"",false,false,(i%50)*200);double queryMs=query.Elapsed.TotalMilliseconds/100;
        using var process=System.Diagnostics.Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;watch.Restart();long peak=0;
        for(int i=0;i<30;i++){await System.Threading.Tasks.Task.Delay(1000);process.Refresh();peak=Math.Max(peak,process.PrivateMemorySize64);}
        double percent=(process.TotalProcessorTime-cpu).TotalSeconds/watch.Elapsed.TotalSeconds/Environment.ProcessorCount*100;
        File.WriteAllText(output,System.Text.Json.JsonSerializer.Serialize(new{cachedArticles=10000,syncSeconds,sourceStatus=service.Sources().Single(s=>s.Id=="bbc").Status,queryMeanMs=queryMs,durationSeconds=watch.Elapsed.TotalSeconds,averageWholeMachineCpuPercent=percent,peakPrivateCommitBytes=peak,scope="isolated reading service; no WPF/audio; private commit is not private working set"},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));return 0;
    }
    private sealed class FixtureHandler:System.Net.Http.HttpMessageHandler{
        internal string Body="";internal bool Fail;internal int Calls;
        protected override async System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request,System.Threading.CancellationToken token){Calls++;Body=await request.Content!.ReadAsStringAsync(token);return new System.Net.Http.HttpResponseMessage(Fail?System.Net.HttpStatusCode.TooManyRequests:System.Net.HttpStatusCode.OK){Content=new System.Net.Http.StringContent(System.Text.Json.JsonSerializer.Serialize(new{choices=new[]{new{finish_reason="stop",message=new{content=System.Text.Json.JsonSerializer.Serialize(new{ChineseTitle="测试标题",ChineseAbstract="测试译文",ChineseSummary="测试摘要"})}}}}))};}
    }

    private static int Layout(string[] args){string output=args[Array.IndexOf(args,"--out")+1];var app=new System.Windows.Application();app.Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary{Source=new Uri("/QuietDesk;component/UI/Theme.xaml",UriKind.Relative)});string dir=Path.Combine(Path.GetTempPath(),"QuietDesk-layout-"+Guid.NewGuid());using var model=new ReadingViewModel(dir);model.Enable();var store=new ReadingStore(Path.Combine(dir,"reading"));store.Put("source","prl",new Source{Id="prl",Name="PRL",Subscribed=true,NextAttempt=DateTimeOffset.Now.AddDays(1)});for(int i=0;i<40;i++)store.Put("article",i.ToString(),new Article{Id=i.ToString(),Title="Long research title: testing clear reading, numerical results and scientific uncertainty",ChineseTitle="测试文章：清晰呈现研究结果与局限",ChineseAbstract="研究比较了不同条件下的实验结果。译文完整保留原始摘要中的数字、化学式与限定条件，帮助读者准确理解研究结论。",SourceName="Physical Review Letters",SourceId="prl",Abstract=new string('摘',160),Summary="测试文章：清晰呈现研究结果与局限\n研究使用给定摘要中的证据，说明研究问题、主要发现和意义。这里不补写全文方法或未提供的局限。",SummaryKey="fixture",PublishedDay=DateTime.Now.ToString("yyyy-MM-dd"),Status="基于摘要／导读"});model.SelectedId="0";var view=new ReadingView(model);view.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.LoadedEvent));if(args.Contains("--subscriptions")||args.Contains("--settings"))typeof(ReadingView).GetMethod("Show",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(view,new object[]{args.Contains("--subscriptions")?1:3});foreach(var size in new[]{(760,560,1.0),(760,560,1.25),(760,560,1.5),(760,560,2.0),(520,560,1.0)}){
        view.Measure(new System.Windows.Size(size.Item1,size.Item2));view.Arrange(new System.Windows.Rect(0,0,size.Item1,size.Item2));view.UpdateLayout();
        var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)(size.Item1*size.Item3),(int)(size.Item2*size.Item3),96*size.Item3,96*size.Item3,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(view);
        var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        string path=Path.Combine(Path.GetDirectoryName(output)!,Path.GetFileNameWithoutExtension(output)+$"-{size.Item1}-{size.Item3:0.##}.png");using(var file=File.Create(path))encoder.Save(file);}
        view.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.UnloadedEvent));app.Shutdown();return 0;}
}
