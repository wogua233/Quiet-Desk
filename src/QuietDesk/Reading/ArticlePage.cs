using System.Collections.Generic;

namespace QuietDesk.Reading;

internal sealed record ArticlePage(List<Article> Articles,int Total,int Offset)
{
    internal string CountText=>$"共 {Total} 篇 · 本页 {Articles.Count} 篇";
    internal string RangeText=>Articles.Count==0?"当前筛选没有文章":$"第 {Offset+1}–{Offset+Articles.Count} 篇 / 共 {Total} 篇";
    internal bool HasPrevious=>Offset>0;
    internal bool HasNext=>Offset+Articles.Count<Total;
}
