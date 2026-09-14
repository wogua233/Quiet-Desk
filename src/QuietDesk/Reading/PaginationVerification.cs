using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace QuietDesk.Reading;

internal static class PaginationVerification
{
    internal static void Checks(string directory,List<string> lines)
    {
        void Check(bool ok,string name){if(!ok)throw new Exception(name);lines.Add("PASS "+name);}
        var store=new ReadingStore(directory);
        store.Put("source","a",new Source{Id="a",Subscribed=true});
        store.Put("source","b",new Source{Id="b",Subscribed=false});
        for(int i=1;i<=401;i++)store.Put("article",i.ToString(),new Article{Id=i.ToString(),SourceId="a",SourceName="A",Title="Article "+i,PublishedDay="2026-09-14",Discovered=DateTimeOffset.Parse("2026-09-14T00:00:00Z").AddSeconds(i),Favorite=i<=3,Read=i<=200});
        store.Put("article","hidden",new Article{Id="hidden",SourceId="b",PublishedDay="2026-09-14"});
        var first=store.QueryPage("2026-09-14",ReadingCatalog.SubscribedFilter,false,false,0);
        var second=store.QueryPage("2026-09-14",ReadingCatalog.SubscribedFilter,false,false,200);
        var last=store.QueryPage("2026-09-14",ReadingCatalog.SubscribedFilter,false,false,400);
        Check(first.Total==401&&first.Articles.Count==200&&first.Articles.First().ListNumber==1&&first.Articles.Last().ListNumber==200&&!first.HasPrevious&&first.HasNext,"filtered total exceeds page size and first page starts at one");
        Check(second.Total==401&&second.Articles.Count==200&&second.Articles.First().ListNumber==201&&second.Articles.Last().ListNumber==400&&second.HasPrevious&&second.HasNext,"second page numbers continue across the filtered result");
        Check(last.CountText=="共 401 篇 · 本页 1 篇"&&last.Articles.Single().ListNumber==401&&!last.HasNext,"last page reports its own count and disables next page");
        var unread=store.QueryPage("2026-09-14",ReadingCatalog.SubscribedFilter,false,false,200,true);
        Check(unread.Total==201&&unread.Articles.Count==1&&unread.Articles.Single().ListNumber==201,"unread count and numbering use the same filter as the page");
        var favorites=store.QueryPage("2000-01-01",ReadingCatalog.SubscribedFilter,false,true,400);
        Check(favorites.Total==3&&favorites.Articles.Count==3&&favorites.Offset==0&&!favorites.HasNext,"favorite count ignores date and obsolete page offset recovers");
        var oldest=store.QueryPage("2026-09-14",ReadingCatalog.SubscribedFilter,false,false,0,order:ArticleOrder.Oldest);
        Check(oldest.Articles[0].Id=="1"&&oldest.Articles[0].ListNumber==1&&first.Articles[0].Id=="401","sequence is assigned after sorting rather than stored with the article");
        Check(!JsonSerializer.Serialize(first.Articles[0]).Contains("ListNumber")&&!JsonSerializer.Serialize(first.Articles[0]).Contains("NumberedTitle"),"list sequence does not enter stored article or AI material");
        var empty=store.QueryPage("2000-01-01",ReadingCatalog.SubscribedFilter,false,false,400);
        Check(empty.CountText=="共 0 篇 · 本页 0 篇"&&empty.Offset==0&&!empty.HasNext&&!empty.HasPrevious,"empty filter has zero counts and no phantom page");
    }
}
