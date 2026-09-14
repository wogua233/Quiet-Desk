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

internal static class PublisherApiVerification
{
    internal static async Task<int> Live(string output)
    {
        using var http=ReadingContent.Client();using var api=new PublisherApis(_=>true);var rows=new List<object>();
        foreach(var source in ReadingCatalog.All().Where(s=>s.Publisher=="aps")){
            try{
                using var response=await http.GetAsync(source.Url);
                var article=ReadingContent.Parse(await ReadingContent.Bounded(response,4_000_000,default),source).First(a=>a.Doi.Length>0);
                article.Abstract="";await api.EnrichAps(article,new ReadingSettings(),default);
                rows.Add(new{source.Name,article.Doi,article.AbstractStatus,abstractCharacters=article.Abstract.Length,article.HasAbstract});
            }catch(Exception e){rows.Add(new{source.Name,error=e.GetType().Name});}
        }
        File.WriteAllText(output,JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));return 0;
    }
    internal static async Task Checks(string directory,List<string> lines)
    {
        void Check(bool ok,string name){if(!ok)throw new Exception(name);lines.Add("PASS "+name);}
        var settings=new ReadingSettings();settings.SetSpringerKey("fixture-springer-key");settings.SetGuardianKey("fixture-guardian-key");
        string json=JsonSerializer.Serialize(settings);
        Check(!json.Contains("fixture-")&&JsonSerializer.Deserialize<ReadingSettings>(json)!.SpringerKey=="fixture-springer-key","publisher keys encrypted separately and absent from serialized plaintext");
        var nature=ReadingCatalog.All().Single(s=>s.Id=="ncomms");var guardian=ReadingCatalog.All().Single(s=>s.Id=="guardian");
        Check(PublisherApis.Enabled(nature,settings)&&PublisherApis.Enabled(guardian,settings)&&!PublisherApis.Enabled(ReadingCatalog.All().Single(s=>s.Id=="jacs"),settings),"publisher API selection restricted to supported built-in sources");
        Check(!PublisherApis.Enabled(new Source{Id="ncomms",Url="https://example.org/custom"},settings),"customized feed does not get silently redirected to a publisher API");
        var handler=new ApiHandler();using var api=new PublisherApis(_=>true,handler);
        var batch=await api.Read(nature,settings,default);
        Check(batch.Articles.Count==51&&handler.Calls==2&&!batch.Partial&&batch.Total==51,"Springer API reads multiple pages within bounded request count");
        var a=batch.Articles[0];Check(a.SourceId=="ncomms"&&a.Doi.StartsWith("10.1038/")&&a.Basis=="基于摘要"&&a.PublishedDay=="2026-09-14","Springer metadata uses DOI identity and online date instead of issue date");
        Check(handler.Addresses.All(u=>u.Host=="api.springernature.com"&&u.AbsolutePath=="/meta/v2/json"&&!u.AbsoluteUri.Contains("fixture-guardian-key"))&&Uri.UnescapeDataString(handler.Addresses[0].Query).Contains("onlinedatefrom:"),"Springer key only sent to official metadata endpoint with date query");
        handler.Guardian=true;batch=await api.Read(guardian,settings,default);
        Check(batch.Articles.Single().Abstract=="Public introduction"&&batch.Articles[0].PublishedDay==DateTimeOffset.Parse("2026-09-14T00:30:00Z").ToLocalTime().ToString("yyyy-MM-dd")&&batch.Articles[0].Basis=="基于导读","Guardian uses requested introduction and first publication timestamp");
        Check(handler.Addresses.Last().Host=="content.guardianapis.com"&&!handler.Addresses.Last().Query.Contains("fixture-springer-key")&&!handler.Addresses.Last().Query.Contains("body"),"Guardian endpoint receives its own key and does not request full body");
        using(var doc=JsonDocument.Parse(JsonSerializer.Serialize(new{records=new[]{new{title="Missing online date",doi="10.1038/unknown",publicationName="Nature Communications",publicationDate="2026-09-14"}},result=new[]{new{total="1"}}}))){
            var missing=PublisherApis.Parse(doc.RootElement,nature,"springer").Articles.Single();
            Check(missing.PublishedDay==null&&!missing.HasAbstract&&missing.AbstractStatus.Length>0,"missing API online date and abstract remain explicit unknowns");
        }
        handler.Code=HttpStatusCode.Unauthorized;bool denied=false;try{await api.Read(nature,settings,default);}catch(IOException e){denied=e.Message.Contains("拒绝授权")&&!e.Message.Contains("fixture-");}Check(denied,"publisher authorization failure is explicit and does not expose key");
        handler.Code=HttpStatusCode.Redirect;try{await api.Read(nature,settings,default);throw new Exception("redirect accepted");}catch(IOException e){Check(e.Message.Contains("跳转"),"publisher API redirects rejected rather than forwarding key");}
        handler.Code=HttpStatusCode.OK;handler.BadJson=true;try{await api.Read(nature,settings,default);throw new Exception("bad JSON accepted");}catch(IOException e){Check(e.Message.Contains("格式异常")&&!e.Message.Contains("fixture-"),"API malformed JSON fails without echoing response or secret");}
        using(var capped=new PublisherApis(_=>false,new ApiHandler())){try{await capped.Read(nature,settings,default);throw new Exception("quota ignored");}catch(IOException e){Check(e.Message.Contains("200 次"),"publisher daily quota stops request before dispatch");}}
        var publisherHandler=new ApiHandler();var rssHandler=new DenyHandler();
        using(var service=new ReadingService(directory,settings,start:false,sourceHandler:rssHandler,publisherHandler:publisherHandler)){
            await service.CheckSource("ncomms");var state=service.Sources().Single(s=>s.Id=="ncomms");
            Check(state.Failures==0&&state.LastChannel=="Springer Nature Meta API"&&rssHandler.Calls==0&&service.Jobs().Count==0,"configured publisher API replaces RSS without creating AI jobs");
        }
        var apsArticle=new Article{SourceId="prx",Doi="10.1103/example"};
        using(var doc=JsonDocument.Parse(JsonSerializer.Serialize(new{data=new{identifiers=new{doi="10.1103/example"},journal=new{id="PRX"},@abstract=new{value="<p>"+new string('a',150)+"</p>"}}}))){
            PublisherApis.ApplyAps(apsArticle,doc.RootElement);
            Check(apsArticle.HasAbstract&&apsArticle.AbstractStatus.Contains("APS Harvest"),"APS JSON abstract is read without requesting a webpage or fulltext");
            apsArticle.Doi="10.1103/other";try{PublisherApis.ApplyAps(apsArticle,doc.RootElement);throw new Exception("mismatched DOI accepted");}catch(IOException){Check(true,"APS metadata must match requested DOI");}
        }
        settings.SetApsKey("fixture-aps-key");
        Check(!JsonSerializer.Serialize(settings).Contains("fixture-aps-key")&&settings.ApsKey=="fixture-aps-key","APS bearer token is separately protected");
        using(var aps=new PublisherApis(_=>true,new ApsHandler())){
            apsArticle.Doi="10.1103/example";apsArticle.Abstract="";
            await aps.EnrichAps(apsArticle,settings,default);
            Check(apsArticle.HasAbstract,"APS requests only official JSON endpoint with bearer header");
        }
        var existing=new Article{Title="Same",Abstract=new string('a',160),Summary="已保存总结",ChineseTitle="原有译题",ChineseAbstract="原有译文",SummaryKey="cached",FeedHash="old-feed",Status="翻译总结已完成"};
        existing.AiContentKey=ReadingAiPolicy.ContentKey(existing);
        ReadingAiPolicy.MergeFeed(existing,new Article{Title="Same",AbstractStatus="API 未提供摘要"});
        Check(existing.Abstract.Length==160&&existing.SummaryKey=="cached"&&existing.Status=="翻译总结已完成"&&existing.ChineseAbstract=="原有译文","switching delivery channel preserves cached abstract and completed translations when omitted by API");
        existing.AiContentKey="";existing.FeedHash="legacy-feed";ReadingAiPolicy.MergeFeed(existing,new Article{Title="Same"});
        Check(existing.SummaryKey=="cached","omitted API abstract also preserves legacy summary cache identity");
        settings.SetSpringerKey("");Check(!PublisherApis.Enabled(nature,settings)&&PublisherApis.Channel(nature,settings)=="官方 RSS","removing publisher key restores official RSS independently of other service keys");
        var migration=new ReadingStore(Path.Combine(directory,"migration"));
        var failed=new Article{Id="failed",SourceId="prl",AbstractStatus="旧网页检查失败",Favorite=true,ChineseTitle="保留译题"};migration.Put("article",failed.Id,failed);
        migration.Put("article","ready",new Article{Id="ready",SourceId="prl",Abstract=new string('a',120),AbstractStatus="已取得摘要",Summary="保留总结"});
        migration.ResetApsAbstractFailures(migrate:true);
        var reset=migration.Find<Article>("article","failed")!;Check(reset.AbstractStatus==""&&reset.Favorite&&reset.ChineseTitle=="保留译题"&&migration.Find<Article>("article","ready")!.Summary=="保留总结","APS migration reopens failed abstract lookup without deleting reading data");
        reset.AbstractStatus="APS 401";migration.Put("article",reset.Id,reset);migration.ResetApsAbstractFailures(migrate:true);
        Check(migration.Find<Article>("article","failed")!.AbstractStatus=="APS 401","APS migration runs once and does not erase authorization failures on restart");
    }
    private sealed class ApsHandler:HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){
            if(request.RequestUri!.Host!="harvest.aps.org"||request.RequestUri.AbsolutePath!="/v2/journals/articles/10.1103/example"||request.Headers.Authorization?.Parameter!="fixture-aps-key"||request.Headers.Authorization?.Scheme!="Bearer"||!request.Headers.Accept.Any(a=>a.MediaType=="application/vnd.tesseract.article+json"))throw new Exception("APS request mismatch");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{data=new{identifiers=new{doi="10.1103/example"},journal=new{id="PRX"},@abstract=new{value=new string('a',160)}}}))});
        }
    }
    private sealed class DenyHandler:HttpMessageHandler
    {
        internal int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Calls++;throw new Exception("unexpected RSS request");}
    }
    private sealed class ApiHandler:HttpMessageHandler
    {
        internal int Calls;internal bool Guardian,BadJson;internal HttpStatusCode Code=HttpStatusCode.OK;
        internal readonly List<Uri> Addresses=new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Calls++;Addresses.Add(request.RequestUri!);
            object payload;
            if(Guardian)payload=new{response=new{status="ok",total=1,results=new[]{new{webTitle="News",sectionId="world",webUrl="https://www.theguardian.com/world/test",webPublicationDate="2026-10-01T00:00:00Z",fields=new{trailText="<p>Public introduction</p>",firstPublicationDate="2026-09-14T00:30:00Z",body="MUST NOT BE USED"}}}}};
            else{bool second=request.RequestUri!.Query.Contains("s=51&");payload=new{records=Enumerable.Range(second?51:1,second?1:50).Select(i=>new{title="Paper "+i,doi="10.1038/test"+i,publicationName="Nature Communications",publicationDate="2026-10-01",onlineDate="2026-09-14",@abstract=new string('a',200)}).ToArray(),result=new[]{new{total="51"}},nextPage="https://example.org/untrusted"};}
            return Task.FromResult(new HttpResponseMessage(Code){Content=new StringContent(BadJson?"malformed fixture-springer-key":JsonSerializer.Serialize(payload))});
        }
    }
}
