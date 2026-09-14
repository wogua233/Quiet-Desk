using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
namespace QuietDesk.Reading;
internal static class ArticleMetadata
{
    internal static async Task Enrich(Article article,HttpClient http,CancellationToken ct)
    {
        try{using var response=await http.GetAsync(article.Url,HttpCompletionOption.ResponseHeadersRead,ct);var bytes=await ReadingContent.Bounded(response,2_000_000,ct);var doc=new HtmlDocument();doc.LoadHtml(Encoding.UTF8.GetString(bytes));
            string Meta(params string[] names)=>doc.DocumentNode.SelectNodes("//meta")?.FirstOrDefault(n=>names.Contains(n.GetAttributeValue("name",n.GetAttributeValue("property","")),StringComparer.OrdinalIgnoreCase))?.GetAttributeValue("content","")??"";
            string date=Meta("citation_online_date","article:published_time");if(DateTimeOffset.TryParse(date,out var d)){article.PublishedDay=date.Length==10?date:d.ToLocalTime().ToString("yyyy-MM-dd");article.DateEvidence="出版社在线发表元数据";}
            string description=article.Basis=="基于导读"?Meta("description","og:description"):Meta("citation_abstract","dc.description");var node=doc.DocumentNode.SelectSingleNode("//*[contains(@class,'abstract-content')]|//*[@id='Abs1-content']|//*[@role='doc-abstract']|//*[contains(@class,'article__abstract')]");if(node!=null)description=node.InnerHtml;
            description=ReadingContent.Plain(description);article.AbstractStatus="已检查出版社摘要元数据";if(description.Length>0&&(!article.HasAbstract||description.Length>article.Abstract.Length))article.Abstract=description[..Math.Min(description.Length,16000)];
        }catch(Exception e)when(e is HttpRequestException or System.IO.IOException or TaskCanceledException){ct.ThrowIfCancellationRequested();article.AbstractStatus="未取得补充摘要："+(e is HttpRequestException h?$"HTTP {h.StatusCode}":"连接或内容读取失败");}
        if(article.HasAbstract||article.Doi.Length==0)return;
        try{using var response=await http.GetAsync("https://api.crossref.org/works/"+Uri.EscapeDataString(article.Doi),HttpCompletionOption.ResponseHeadersRead,ct);var bytes=await ReadingContent.Bounded(response,1_000_000,ct);using var doc=JsonDocument.Parse(bytes);if(doc.RootElement.GetProperty("message").TryGetProperty("abstract",out var a)){string text=ReadingContent.Plain(a.GetString()??"");article.Abstract=text[..Math.Min(text.Length,16000)];}}catch(Exception e)when(e is HttpRequestException or System.IO.IOException or TaskCanceledException or JsonException){ct.ThrowIfCancellationRequested();article.AbstractStatus="未取得补充摘要："+(e is HttpRequestException h?$"HTTP {h.StatusCode}":"连接或内容读取失败");}
    }
}
