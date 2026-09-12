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
        using var stream=new MemoryStream(bytes);using var xr=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4_000_000});var doc=XDocument.Load(xr);var list=new List<Article>();
        foreach(var item in doc.Descendants().Where(x=>x.Name.LocalName is "item" or "entry").Take(1000)){
            string V(params string[] names)=>item.Elements().FirstOrDefault(x=>names.Contains(x.Name.LocalName))?.Value.Trim()??"";
            var link=item.Elements().FirstOrDefault(x=>x.Name.LocalName=="link"&&(x.Attribute("rel")?.Value is null or "alternate"));var url=Canonical(link?.Attribute("href")?.Value??link?.Value??"");if(url.Length==0)continue;
            string title=Plain(V("title"));if(title.Length==0)continue;var doi=Regex.Match(V("identifier","doi")+" "+url,@"10\.\d{4,9}/[^\s?#<>]+",RegexOptions.IgnoreCase).Value.TrimEnd('.').ToLowerInvariant();
            string raw=V("published","pubDate","date");string? day=null;
            if(Regex.IsMatch(raw,@"^\d{4}-\d{2}-\d{2}$"))day=raw;
            else if(DateTimeOffset.TryParse(raw,CultureInfo.InvariantCulture,DateTimeStyles.AllowWhiteSpaces,out var when))day=when.ToLocalTime().ToString("yyyy-MM-dd");
            var a=new Article{Id=ReadingCatalog.Hash(doi.Length>0?doi:source.Id+":"+url),Doi=doi,SourceId=source.Id,SourceName=source.Name,Title=title,Url=url,Abstract=Plain(V("description","summary","encoded","content")),PublishedDay=day,DateEvidence=raw.Length==0?"未知":"订阅源发表日期"};if(a.Abstract.Length>16000)a.Abstract=a.Abstract[..16000];list.Add(a);
        }return list;
    }
    internal static Extracted ExtractHtml(string html,string publisher)
    {
        var d=new HtmlDocument();d.LoadHtml(html);
        var node=publisher switch{"aps"=>d.DocumentNode.SelectSingleNode("//*[contains(@class,'article-body')]"),"nature"=>d.DocumentNode.SelectSingleNode("//*[@data-component='article-body']|//*[contains(@class,'c-article-body')]"),"acs"=>d.DocumentNode.SelectSingleNode("//*[contains(@class,'articleBody')]"),"science"=>d.DocumentNode.SelectSingleNode("//*[@role='doc-article']|//*[contains(@class,'article__body')]"),_=>d.DocumentNode.SelectSingleNode("//article")};
        if(node==null)return new(){Error="需在浏览器访问：未取得可识别全文。"};
        foreach(var n in node.SelectNodes(".//script|.//style|.//nav|.//aside")??Enumerable.Empty<HtmlNode>())n.Remove();
        var text=new StringBuilder();foreach(var n in node.SelectNodes(".//h2|.//h3|.//p")??Enumerable.Empty<HtmlNode>()){var t=Plain(n.InnerHtml);if(t.Length>0)text.AppendLine(n.Name.StartsWith("h")?"\n【"+t+"】":t);}
        string result=text.ToString();bool gated=Regex.IsMatch(result,@"subscribe to (read|access)|purchase (this|the) article|access through your institution|sign in to access",RegexOptions.IgnoreCase);
        if(result.Length<1000||gated)return new(){Error="需在浏览器访问：全文不足或访问受限。"};
        return new(){Text=result.Length>200000?result[..200000]:result,Basis="网页章节",Complete=false};
    }
}
