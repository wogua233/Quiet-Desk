using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
namespace QuietDesk.Reading;

public sealed class ReadingSettings
{
    public bool Enabled {get;set;}
    public string Endpoint {get;set;}="";
    public string Model {get;set;}="";
    public string ProtectedKey {get;set;}="";
    public string AbstractConsent {get;set;}="";
    public int AbstractModeVersion {get;set;}
    public bool Automatic {get;set;}
    public int DailyLimit {get;set;}=10;
    [System.Text.Json.Serialization.JsonIgnore]
    public string Key {get {try{return ProtectedKey.Length==0?"":Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(ProtectedKey),null,DataProtectionScope.CurrentUser));}catch{return "";}}}
    public void SetKey(string key)=>ProtectedKey=key.Length==0?"":Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(key),null,DataProtectionScope.CurrentUser));
    [System.Text.Json.Serialization.JsonIgnore]
    public bool Ready=>Uri.TryCreate(Endpoint,UriKind.Absolute,out var u)&&u.Scheme=="https"&&Model.Length>0&&Key.Length>0;
}
public sealed class Source
{
    public string Id {get;set;}="";public string Name {get;set;}="";public string Url {get;set;}="";public string Publisher {get;set;}="";
    public bool Subscribed {get;set;} public string Status {get;set;}="尚未更新";public DateTimeOffset? LastSuccess {get;set;}
    public string ETag {get;set;}="";public string Modified {get;set;}="";public DateTimeOffset? NextAttempt {get;set;} public int Failures {get;set;} public int ArticleCount {get;set;} public int ParserVersion {get;set;}
    public override string ToString()=>Name;
}
public sealed class Article
{
    public string Id {get;set;}="";public string SourceId {get;set;}="";public string SourceName {get;set;}="";public string Title {get;set;}="";
    public string ChineseTitle {get;set;}="";public string Url {get;set;}="";public string Doi {get;set;}="";public string Abstract {get;set;}="";
    public string? PublishedDay {get;set;} public string DateEvidence {get;set;}="";public DateTimeOffset Discovered {get;set;}=DateTimeOffset.Now;
    public bool Read {get;set;} public bool Favorite {get;set;} public string Summary {get;set;}="";
    public string SummaryKey {get;set;}="";public string Status {get;set;}="待生成";
    public string Basis {get;set;}="基于摘要";
    public string AbstractStatus {get;set;}="";
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasAbstract=>Abstract.Length>=100&&!Abstract.TrimEnd().EndsWith("…")&&!Abstract.TrimEnd().EndsWith("...");

    public string DisplayTitle=>(Read?"":"● ")+(Favorite?"★ ":"")+(ChineseTitle.Length>0?ChineseTitle:Title);
    public string DisplayMeta=>$"{SourceName} · {PublishedDay??"日期待确认"}";
    public string Display=>$"{DisplayTitle}\n{DisplayMeta} · {Status}";
}
internal static class ReadingCatalog
{
    internal static List<Source> All()=>new[]{
        S("prl","PRL","https://feeds.aps.org/rss/recent/prl.xml","aps"),S("prx","PRX","https://feeds.aps.org/rss/recent/prx.xml","aps"),S("prb","PRB","https://feeds.aps.org/rss/recent/prb.xml","aps"),S("pre","PRE","https://feeds.aps.org/rss/recent/pre.xml","aps"),
        S("jacs","JACS","https://pubs.acs.org/action/showFeed?type=axatoc&feed=rss&jc=jacsat","acs"),
        S("nature","Nature","https://www.nature.com/nature.rss","nature"),S("science","Science","https://www.science.org/action/showFeed?type=etoc&feed=rss&jc=science","science"),
        S("ncomms","Nature Communications","https://www.nature.com/ncomms.rss","nature"),S("nchem","Nature Chemistry","https://www.nature.com/nchem.rss","nature"),
        S("sciam","Scientific American","https://www.scientificamerican.com/platform/syndication/rss/","news"),S("bbc","BBC World","https://feeds.bbci.co.uk/news/world/rss.xml","news"),S("guardian","The Guardian World","https://www.theguardian.com/world/rss","news")}.ToList();
    private static Source S(string id,string name,string url,string publisher)=>new(){Id=id,Name=name,Url=url,Publisher=publisher};
    internal static string Hash(string s)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
    internal static bool Http(string s)=>Uri.TryCreate(s,UriKind.Absolute,out var u)&&(u.Scheme is "https" or "http")&&string.IsNullOrEmpty(u.UserInfo);
}
