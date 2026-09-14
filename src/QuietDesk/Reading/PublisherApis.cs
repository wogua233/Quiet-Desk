using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace QuietDesk.Reading;

internal sealed record PublisherBatch(List<Article> Articles,string Channel,int Total,bool Partial);

internal sealed class PublisherApis:IDisposable
{
    private readonly HttpClient client;
    private readonly SemaphoreSlim gate=new(1);
    private readonly Func<string,bool> reserve;
    private readonly TimeSpan interval;
    private DateTimeOffset lastRequest;
    internal PublisherApis(Func<string,bool> reserve,HttpMessageHandler? handler=null)
    {
        this.reserve=reserve;interval=handler==null?TimeSpan.FromSeconds(1):TimeSpan.Zero;
        // API keys belong only to these endpoints, never to redirected hosts.
        client=new HttpClient(handler??new HttpClientHandler{AllowAutoRedirect=false,AutomaticDecompression=System.Net.DecompressionMethods.All}){Timeout=TimeSpan.FromSeconds(30)};
        client.DefaultRequestHeaders.UserAgent.ParseAdd("QuietDesk/0.5 (personal-feed-reader)");
    }
    internal static string Provider(Source s)
    {
        var official=ReadingCatalog.All().FirstOrDefault(x=>x.Id==s.Id);
        if(official==null||official.Url!=s.Url)return "";
        return s.Id is "nature" or "ncomms" or "nchem"?"springer":s.Id=="guardian"?"guardian":"";
    }
    internal static bool Enabled(Source s,ReadingSettings settings)=>Provider(s) switch{"springer"=>settings.SpringerKey.Length>0,"guardian"=>settings.GuardianKey.Length>0,_=>false};
    internal static string Channel(Source s,ReadingSettings settings)=>Enabled(s,settings)?Provider(s)=="springer"?"Springer Nature Meta API":"Guardian Content API":ReadingCatalog.All().Any(x=>x.Id==s.Id&&x.Url==s.Url)?"官方 RSS":"自定义 RSS／Atom";
    internal static string Description(Source s,ReadingSettings settings)=>"接入："+Channel(s,settings)+(s.Publisher=="aps"?" · 缺失摘要通过 APS Harvest API 补充" :!Enabled(s,settings)&&Provider(s).Length>0?" · 可在阅读设置配置独立 API 密钥":"");

    internal static bool ApsArticle(Article a)=>(a.SourceId is "prl" or "prx" or "prb" or "pre") && a.Doi.StartsWith("10.1103/",StringComparison.OrdinalIgnoreCase);
    internal async Task EnrichAps(Article a,ReadingSettings settings,CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try{
            var wait=interval-(DateTimeOffset.UtcNow-lastRequest);if(wait>TimeSpan.Zero)await Task.Delay(wait,ct);
            if(!reserve("aps")){a.AbstractStatus="APS API 今日已达到应用请求上限";return;}
            lastRequest=DateTimeOffset.UtcNow;
            using var request=new HttpRequestMessage(HttpMethod.Get,"https://harvest.aps.org/v2/journals/articles/10.1103/"+Uri.EscapeDataString(a.Doi[8..]));
            request.Headers.Accept.ParseAdd("application/vnd.tesseract.article+json");
            string key=settings.ApsKey;if(key.Length>0)request.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",key);
            using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
            if(!response.IsSuccessStatusCode){a.AbstractStatus=$"APS 摘要 API 未提供该文章（HTTP {(int)response.StatusCode}；非开放内容可能需要 APS 授权）";return;}
            using var doc=JsonDocument.Parse(await ReadingContent.Bounded(response,2_000_000,ct));
            ApplyAps(a,doc.RootElement);
        }catch(Exception e)when(e is HttpRequestException or System.IO.IOException or JsonException or TaskCanceledException){ct.ThrowIfCancellationRequested();a.AbstractStatus="APS 摘要 API 连接或响应失败";}
        finally{gate.Release();}
    }
    internal static void ApplyAps(Article a,JsonElement root)
    {
        if(root.ValueKind!=JsonValueKind.Object||!root.TryGetProperty("data",out var data)||data.ValueKind!=JsonValueKind.Object||!data.TryGetProperty("identifiers",out var ids)||!Text(ids,"doi").Equals(a.Doi,StringComparison.OrdinalIgnoreCase))throw new IOException("APS API 返回的文章身份不匹配。");
        if(!data.TryGetProperty("journal",out var journal)||!Text(journal,"id").Equals(a.SourceId,StringComparison.OrdinalIgnoreCase))throw new IOException("APS API 返回的刊物不匹配。");
        string value=data.TryGetProperty("abstract",out var abs)?Text(abs,"value"):"";
        value=ReadingContent.Plain(value);if(value.Length>a.Abstract.Length)a.Abstract=value[..Math.Min(16000,value.Length)];
        a.AbstractStatus=a.HasAbstract?"已通过 APS Harvest API 获取摘要":"APS API 未提供足够摘要";
    }

    internal async Task<PublisherBatch> Read(Source source,ReadingSettings settings,CancellationToken ct)
    {
        string provider=Provider(source),key=provider=="springer"?settings.SpringerKey:settings.GuardianKey;
        if(!Enabled(source,settings))throw new IOException("该来源尚未配置出版社 API 密钥。");
        await gate.WaitAsync(ct);
        try{
            var articles=new Dictionary<string,Article>();int total=0,received=0;bool partial=false;
            for(int page=1;page<=10;page++){
                var wait=interval-(DateTimeOffset.UtcNow-lastRequest);if(wait>TimeSpan.Zero)await Task.Delay(wait,ct);
                if(!reserve(provider))throw new IOException("该出版社 API 今日已达到应用的 200 次请求上限，明日重试；已有文章保留。");
                lastRequest=DateTimeOffset.UtcNow;
                using var request=new HttpRequestMessage(HttpMethod.Get,Address(source,provider,key,page,DateTime.Today));
                using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
                if(!response.IsSuccessStatusCode)throw new IOException((int)response.StatusCode switch{401 or 403=>"出版社 API 拒绝授权，请检查该服务的密钥及开通范围。",429=>"出版社 API 限流，请等待自动退避后重试。",>=300 and <400=>"出版社 API 返回跳转，已停止以保护密钥。",_=>$"出版社 API 请求失败（HTTP {(int)response.StatusCode}）。"});
                using var doc=JsonDocument.Parse(await ReadingContent.Bounded(response,4_000_000,ct));
                var parsed=Parse(doc.RootElement,source,provider);total=parsed.Total;received+=parsed.RawCount;
                foreach(var a in parsed.Articles)articles[a.Id]=a;
                if(received>=total)break;
                if(parsed.RawCount==0)throw new IOException("出版社 API 分页提前结束，未将不完整响应标记为成功。");
                if(page==10)partial=true;
            }
            return new(articles.Values.ToList(),Channel(source,settings),total,partial);
        }catch(HttpRequestException){throw new IOException("出版社 API 连接失败，请检查网络后重试。");}
        catch(JsonException){throw new IOException("出版社 API 返回格式异常，已有文章保留。");}
        catch(TaskCanceledException)when(!ct.IsCancellationRequested){throw new IOException("出版社 API 请求超时，请稍后重试。");}
        finally{gate.Release();}
    }
    internal static string Address(Source source,string provider,string key,int page,DateTime today)
    {
        string from=today.AddDays(-6).ToString("yyyy-MM-dd"),to=today.ToString("yyyy-MM-dd");
        if(provider=="springer"){
            string journal=ReadingCatalog.All().Single(s=>s.Id==source.Id).Name;
            string query=$"journal:\"{journal}\" onlinedatefrom:{from} onlinedateto:{to} sort:date";
            return "https://api.springernature.com/meta/v2/json?q="+Uri.EscapeDataString(query)+$"&s={(page-1)*50+1}&p=50&api_key="+Uri.EscapeDataString(key);
        }
        return $"https://content.guardianapis.com/search?section=world&from-date={from}&to-date={to}&use-date=first-publication&order-by=newest&page-size=50&page={page}&show-fields=trailText,firstPublicationDate&api-key="+Uri.EscapeDataString(key);
    }
    internal sealed record Parsed(List<Article> Articles,int Total,int RawCount);
    internal static Parsed Parse(JsonElement root,Source source,string provider)
    {
        JsonElement records;int total;
        if(provider=="springer"){
            if(!root.TryGetProperty("records",out records)||!root.TryGetProperty("result",out var result)||result.ValueKind!=JsonValueKind.Array||result.GetArrayLength()==0)throw new IOException("Springer API 响应缺少目录或分页信息。");
            total=Number(result[0],"total");
        }else{
            if(!root.TryGetProperty("response",out var response)||Text(response,"status")!="ok"||!response.TryGetProperty("results",out records))throw new IOException("Guardian API 未返回有效目录。");
            total=Number(response,"total");
        }
        if(records.ValueKind!=JsonValueKind.Array||total<0)throw new IOException("出版社 API 分页格式异常。");
        var articles=new List<Article>();
        foreach(var item in records.EnumerateArray()){
            string title,doi="",url,description,date,evidence;
            if(provider=="springer"){
                if(!Text(item,"publicationName").Equals(source.Name,StringComparison.OrdinalIgnoreCase))throw new IOException("Springer API 返回了其他刊物，已停止导入。");
                doi=Text(item,"doi").ToLowerInvariant();if(doi.Length==0)doi=Text(item,"identifier").Replace("doi:","",StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
                if(!doi.StartsWith("10."))throw new IOException("Springer API 文章缺少有效 DOI。");
                url="https://doi.org/"+doi;title=Text(item,"title");description=Text(item,"abstract");date=Text(item,"onlineDate");evidence="Springer Meta API 在线发表日期";
                if(item.TryGetProperty("url",out var urls)&&urls.ValueKind==JsonValueKind.Array){var link=urls.EnumerateArray().FirstOrDefault(x=>Text(x,"format")=="html"&&ReadingCatalog.Http(Text(x,"value")));if(link.ValueKind!=JsonValueKind.Undefined)url=Text(link,"value");}
            }else{
                if(Text(item,"sectionId")!="world")throw new IOException("Guardian API 返回了其他栏目，已停止导入。");
                url=ReadingContent.Canonical(Text(item,"webUrl"));title=Text(item,"webTitle");
                var fields=item.TryGetProperty("fields",out var f)?f:default;description=Text(fields,"trailText");date=Text(fields,"firstPublicationDate");evidence="Guardian API 首次发表日期";
            }
            if(!ReadingCatalog.Http(url)||title.Length==0)throw new IOException("出版社 API 文章缺少标题或有效链接。");
            var a=new Article{Id=ReadingCatalog.Hash(doi.Length>0?doi:source.Id+":"+ReadingContent.Canonical(url)),SourceId=source.Id,SourceName=source.Name,Doi=doi,Url=ReadingContent.Canonical(url),Title=ReadingContent.Plain(title),Abstract=ReadingContent.Plain(description),Basis=provider=="springer"?"基于摘要":"基于导读",PublishedDay=Day(date),DateEvidence=Day(date)==null?"未知：API 未提供完整首次在线发表日期":evidence};
            if(a.Abstract.Length>16000)a.Abstract=a.Abstract[..16000];
            if(!a.HasAbstract)a.AbstractStatus="出版社 API 未提供足够的摘要／导读";
            articles.Add(a);
        }
        return new(articles,total,records.GetArrayLength());
    }
    private static string Text(JsonElement e,string key)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()??"":"";
    private static int Number(JsonElement e,string key){if(!e.TryGetProperty(key,out var v)||!int.TryParse(v.ToString(),out int n))throw new IOException("出版社 API 缺少总数。");return n;}
    private static string? Day(string value){if(value.Length==10&&DateTime.TryParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _))return value;if(value.Length>10&&DateTimeOffset.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.None,out var d))return d.ToLocalTime().ToString("yyyy-MM-dd");return null;}
    public void Dispose()=>client.Dispose();
}
