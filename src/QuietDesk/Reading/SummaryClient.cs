using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
namespace QuietDesk.Reading;

internal sealed class AiApiException:IOException
{
    internal int StatusCode {get;}
    internal TimeSpan RetryDelay {get;}
    internal AiApiException(int code,TimeSpan? delay=null):base(code==429?"API限流，已暂缓队列。":$"AI服务返回HTTP {code}，请检查模型、密钥或余额。")
    {StatusCode=code;RetryDelay=delay??TimeSpan.FromMinutes(1);}
}
internal sealed class SummaryClient:IDisposable
{
    private readonly HttpClient client;
    internal SummaryClient(HttpMessageHandler? handler=null)=>client=new(handler??new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(90)};
    private static string BaseAddress(ReadingSettings settings)
    {
        string endpoint=settings.Endpoint.TrimEnd('/');
        const string suffix="/chat/completions";
        return endpoint.EndsWith(suffix,StringComparison.OrdinalIgnoreCase)?endpoint[..^suffix.Length]:endpoint;
    }
    private static void Authorize(HttpRequestMessage request,ReadingSettings settings){
        request.Version=System.Net.HttpVersion.Version20;request.VersionPolicy=HttpVersionPolicy.RequestVersionOrLower;
        string key=settings.Key;
        if(!Regex.IsMatch(key,@"^[A-Za-z0-9._~+/=-]+$"))throw new IOException("密钥包含无效字符，请重新填写。");
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
    }
    internal async Task<string[]> Models(ReadingSettings settings,CancellationToken ct)
    {
        if(!Uri.TryCreate(settings.Endpoint,UriKind.Absolute,out var uri)||uri.Scheme!="https"||settings.Key.Length==0)throw new IOException("请先配置HTTPS服务地址和密钥。");
        using var request=new HttpRequestMessage(HttpMethod.Get,BaseAddress(settings)+"/models");Authorize(request,settings);
        using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
        if(!response.IsSuccessStatusCode)throw new AiApiException((int)response.StatusCode);
        using var doc=JsonDocument.Parse(await ReadingContent.Bounded(response,256000,ct));
        return doc.RootElement.GetProperty("data").EnumerateArray().Select(x=>x.GetProperty("id").GetString()??"").Where(x=>x.Length>0).ToArray();
    }
    internal async Task<BilingualResult> Translate(ReadingSettings settings,Article article,string[] fields,Action<AiUsage>? usage,CancellationToken ct)
    {
        if(!settings.Ready)throw new IOException("请先配置HTTPS API地址、模型与密钥。");
        var body=new Dictionary<string,object>{
            ["model"]=settings.Model,
            ["messages"]=new[]{new{role="system",content=ReadingAiPolicy.Prompt},new{role="user",content=JsonSerializer.Serialize(new{requested_fields=fields,title=article.Title,basis=article.Basis,abstract_text=article.HasAbstract?article.Abstract:""})}},
            ["response_format"]=new{type="json_object"},
            ["max_tokens"]=article.HasAbstract?Math.Clamp(article.Abstract.Length*2+1000,2000,12000):512,
            ["temperature"]=0.2
        };
        if(new Uri(settings.Endpoint).Host.Equals("api.deepseek.com",StringComparison.OrdinalIgnoreCase))body["thinking"]=new{type="disabled"};
        using var request=new HttpRequestMessage(HttpMethod.Post,BaseAddress(settings)+"/chat/completions");Authorize(request,settings);
        request.Content=new StringContent(JsonSerializer.Serialize(body),Encoding.UTF8,"application/json");
        using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
        if(!response.IsSuccessStatusCode)throw new AiApiException((int)response.StatusCode,response.Headers.RetryAfter?.Delta);
        using var doc=JsonDocument.Parse(await ReadingContent.Bounded(response,512000,ct));
        var receipt=new AiUsage{Model=settings.Model};
        if(doc.RootElement.TryGetProperty("usage",out var u)){
            long? Number(string key)=>u.TryGetProperty(key,out var v)&&v.TryGetInt64(out var n)?n:null;
            receipt.InputTokens=Number("prompt_tokens");receipt.OutputTokens=Number("completion_tokens");receipt.CachedTokens=Number("prompt_cache_hit_tokens");
            if(receipt.CachedTokens==null&&u.TryGetProperty("prompt_tokens_details",out var details)&&details.TryGetProperty("cached_tokens",out var cached)&&cached.TryGetInt64(out var tokens))receipt.CachedTokens=tokens;
        }
        usage?.Invoke(receipt);
        var choice=doc.RootElement.GetProperty("choices")[0];
        if(choice.TryGetProperty("finish_reason",out var reason)&&reason.GetString()!="stop")throw new IOException("AI输出被截断或未正常完成，旧结果已保留。");
        string content=choice.GetProperty("message").GetProperty("content").GetString()??"";
        return Parse(content,fields);
    }
    internal static BilingualResult Parse(string content,string[] fields)
    {
        if(string.IsNullOrWhiteSpace(content))throw new IOException("AI服务返回空内容，未标记完成。");
        try{
            using var doc=JsonDocument.Parse(content);
            var result=new BilingualResult();
            foreach(string field in fields){
                if(!doc.RootElement.TryGetProperty(field,out var value)||value.ValueKind!=JsonValueKind.String)throw new IOException("AI返回缺少要求的翻译字段。");
                string text=value.GetString()?.Trim()??"";
                if(text.Length==0||text.Length>40000||!Regex.IsMatch(text,@"[\p{IsCJKUnifiedIdeographs}]"))throw new IOException("AI返回字段为空、过长或缺少中文，未标记完成。");
                switch(field){case "ChineseTitle":result.ChineseTitle=text;break;case "ChineseAbstract":result.ChineseAbstract=text;break;case "ChineseSummary":result.ChineseSummary=text;break;default:throw new IOException("未知翻译字段。");}
            }
            return result;
        }catch(JsonException){throw new IOException("AI返回格式不是完整JSON，旧结果已保留。");}
    }
    // Compatibility helper for the pre-existing small protocol tests.
    internal async Task<string> Ask(ReadingSettings settings,string material,CancellationToken ct)
    {
        var result=await Translate(settings,new Article{Title="连接测试",Abstract=material},new[]{"ChineseTitle","ChineseSummary"},null,ct);
        return result.ChineseTitle+"\n"+result.ChineseSummary;
    }
    public void Dispose()=>client.Dispose();
}
