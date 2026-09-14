using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using HtmlAgilityPack;
namespace QuietDesk.Reading;
internal static class ReadingContent
{
    internal static HttpClient Client(){var c=new HttpClient(new HttpClientHandler{AutomaticDecompression=System.Net.DecompressionMethods.All}){Timeout=TimeSpan.FromSeconds(30)};c.DefaultRequestHeaders.UserAgent.ParseAdd("QuietDesk/0.4 (personal-feed-reader)");return c;}
    internal static async Task<byte[]> Bounded(HttpResponseMessage response,int limit,CancellationToken ct){response.EnsureSuccessStatusCode();if(response.Content.Headers.ContentLength>limit)throw new IOException("内容超出大小限制。");using var input=await response.Content.ReadAsStreamAsync(ct);using var output=new MemoryStream();var buffer=new byte[16384];int n;while((n=await input.ReadAsync(buffer,ct))>0){if(output.Length+n>limit)throw new IOException("内容超出大小限制。");output.Write(buffer,0,n);}return output.ToArray();}
    internal static string Plain(string html){var d=new HtmlDocument();d.LoadHtml(html);foreach(var n in d.DocumentNode.SelectNodes("//script|//style|//nav|//footer|//header")??Enumerable.Empty<HtmlNode>())n.Remove();return Regex.Replace(HtmlEntity.DeEntitize(d.DocumentNode.InnerText),@"\s+"," ").Trim();}
    internal static string Canonical(string url){if(!ReadingCatalog.Http(url))return "";var u=new UriBuilder(url){Fragment=""};u.Query=string.Join("&",u.Query.TrimStart('?').Split('&',StringSplitOptions.RemoveEmptyEntries).Where(s=>!s.StartsWith("utm_",StringComparison.OrdinalIgnoreCase)));return u.Uri.AbsoluteUri;}
    internal static List<Article> Parse(byte[] bytes,Source source)
    {
        // Skip declarations without evaluating DTDs or resolving external resources.
        using var stream=new MemoryStream(bytes);using var xr=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Ignore,XmlResolver=null,MaxCharactersInDocument=4_000_000});
        xr.MoveToContent();
        if(xr.LocalName.Equals("html",StringComparison.OrdinalIgnoreCase))throw new IOException("来源返回了网页，而不是订阅内容（可能是验证、登录或临时错误页）。请稍后重试；已有文章仍可阅读。");
        if(xr.LocalName is not ("rss" or "RDF" or "feed"))throw new IOException("返回内容不是 RSS／Atom 订阅源。");
        var doc=XDocument.Load(xr);var list=new List<Article>();
        foreach(var item in doc.Descendants().Where(x=>x.Name.LocalName is "item" or "entry").Take(1000)){
            string V(params string[] names)=>item.Elements().FirstOrDefault(x=>names.Contains(x.Name.LocalName))?.Value.Trim()??"";
            var link=item.Elements().FirstOrDefault(x=>x.Name.LocalName=="link"&&(x.Attribute("rel")?.Value is null or "alternate"));var url=Canonical(link?.Attribute("href")?.Value??link?.Value??"");if(url.Length==0)continue;
            string title=Plain(V("title"));if(title.Length==0)continue;var doi=Regex.Match(V("identifier","doi")+" "+url,@"10\.\d{4,9}/[^\s?#<>]+",RegexOptions.IgnoreCase).Value.TrimEnd('.').ToLowerInvariant();
            string raw=V("published","pubDate","date");string? day=null;
            if(Regex.IsMatch(raw,@"^\d{4}-\d{2}-\d{2}$"))day=raw;
            else if(DateTimeOffset.TryParse(raw,CultureInfo.InvariantCulture,DateTimeStyles.AllowWhiteSpaces,out var when))day=when.ToLocalTime().ToString("yyyy-MM-dd");
            string excerpt=V("description","summary");
            // Nature's official feed uses content:encoded for a short standfirst, not article body.
            if(source.Publisher=="nature"&&new Uri(source.Url).Host=="www.nature.com"&&excerpt.Length==0){
                var fragment=new HtmlDocument();fragment.LoadHtml(V("encoded"));
                var header=fragment.DocumentNode.SelectSingleNode("//p");
                if(header!=null&&header.InnerText.Contains("Published online:")){header.Remove();excerpt=fragment.DocumentNode.InnerHtml;}
            }

            if(source.Publisher=="aps"){var fragment=new HtmlDocument();fragment.LoadHtml(excerpt);var paragraph=fragment.DocumentNode.SelectSingleNode("//p");if(paragraph!=null)excerpt=paragraph.InnerHtml;}
            var a=new Article{Id=ReadingCatalog.Hash(doi.Length>0?doi:source.Id+":"+url),Doi=doi,SourceId=source.Id,SourceName=source.Name,Title=title,Url=url,Basis=source.Publisher is "news" or "nature"?"基于导读":"基于摘要",Abstract=Plain(excerpt),PublishedDay=day,DateEvidence=raw.Length==0?"未知":"订阅源发表日期"};if(a.Abstract.Length>16000)a.Abstract=a.Abstract[..16000];list.Add(a);
        }return list;
    }
}
