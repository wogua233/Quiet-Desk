using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace QuietDesk;

public sealed class RadioDirectory : IDisposable
{
    private readonly HttpClient http=new(){Timeout=TimeSpan.FromSeconds(12)};
    private readonly string cache;
    public bool Cached {get;private set;}
    public RadioDirectory(string directory){cache=Path.Combine(directory,"radio-cache.json");http.DefaultRequestHeaders.UserAgent.ParseAdd("QuietDesk/0.1 (+desktop-focus-player)");}
    public static bool IsHttp(string? url)=>Uri.TryCreate(url,UriKind.Absolute,out var uri)&&uri.Scheme is "https" or "http";
    public async Task<List<Station>> Search(string name,string tag,CancellationToken token)
    {
        Cached=false;if(tag.StartsWith("domestic",StringComparison.Ordinal))return FilterDomestic(Domestic(),name,tag);Exception? last=null;
        var query=$"hidebroken=true&codec=MP3&limit=60&order=votes&reverse=true&name={Uri.EscapeDataString(name)}&tag={Uri.EscapeDataString(tag)}";
        foreach(var host in new[]{"de1.api.radio-browser.info","nl1.api.radio-browser.info"}) {
            try {
                var text=await http.GetStringAsync($"https://{host}/json/stations/search?{query}",token);
                if(text.Length>2_000_000)throw new IOException("电台目录响应过大。");
                using var doc=JsonDocument.Parse(text);var result=new List<Station>();
                foreach(var item in doc.RootElement.EnumerateArray()) {
                    string S(string key)=>item.TryGetProperty(key,out var p)?p.GetString()??"":"";
                    var url=S("url_resolved");if(!IsHttp(url))continue;
                    result.Add(new Station{Id=S("stationuuid"),Name=S("name"),Url=url,Tags=S("tags"),Country=S("country"),Bitrate=item.TryGetProperty("bitrate",out var b)?b.GetInt32():0});
                }
                try{File.WriteAllText(cache,JsonSerializer.Serialize(result));}catch(IOException){}
                return result;
            }catch(Exception e) when(e is HttpRequestException or TaskCanceledException or IOException or JsonException) {if(token.IsCancellationRequested)throw;last=e;}
        }
        if(File.Exists(cache)) {
            try{var list=JsonSerializer.Deserialize<List<Station>>(File.ReadAllText(cache))??new();Cached=true;return list.Where(s=>s.Name.Contains(name,StringComparison.OrdinalIgnoreCase)&&(tag.Length==0||s.Tags.Contains(tag,StringComparison.OrdinalIgnoreCase))).ToList();}catch(JsonException){}
        }
        throw new IOException("电台目录暂时无法连接；你仍可使用离线声音或添加 MP3 电台直链。",last);
    }
    public static List<Station> Domestic()=>JsonSerializer.Deserialize<List<Station>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Assets","domestic-radio.json")))??new();
    internal static List<Station> FilterDomestic(IEnumerable<Station> stations,string name,string tag){
        string genre=tag switch{"domestic:jazz"=>"爵士","domestic:classical"=>"古典","domestic:pop"=>"流行",_=>""};
        return stations.Where(s=>(s.Name+" "+s.Tags).Contains(name,StringComparison.OrdinalIgnoreCase)&&(genre.Length==0||s.Tags.Contains(genre,StringComparison.Ordinal))).ToList();
    }
    public void Dispose()=>http.Dispose();
}
