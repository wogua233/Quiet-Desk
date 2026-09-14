using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Data.Sqlite;
namespace QuietDesk.Reading;
internal sealed class ReadingStore
{
    internal string DirectoryPath {get;}
    private readonly object gate=new();
    internal ReadingStore(string directory){DirectoryPath=directory;System.IO.Directory.CreateDirectory(directory);using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="PRAGMA journal_mode=WAL; CREATE TABLE IF NOT EXISTS objects(kind TEXT NOT NULL,id TEXT NOT NULL,json TEXT NOT NULL,PRIMARY KEY(kind,id)); CREATE TABLE IF NOT EXISTS usage(day TEXT PRIMARY KEY,n INTEGER NOT NULL); CREATE INDEX IF NOT EXISTS article_url ON objects(json_extract(json,'$.Url')) WHERE kind='article'; CREATE INDEX IF NOT EXISTS article_day ON objects(json_extract(json,'$.PublishedDay')) WHERE kind='article';";cmd.ExecuteNonQuery();}
    private SqliteConnection Open(){var c=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=Path.Combine(DirectoryPath,"reading.db"),Pooling=false}.ToString());c.Open();return c;}
    internal List<T> Load<T>(string kind){lock(gate){using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT json FROM objects WHERE kind=$k";cmd.Parameters.AddWithValue("$k",kind);using var r=cmd.ExecuteReader();var list=new List<T>();while(r.Read()){var item=JsonSerializer.Deserialize<T>(r.GetString(0));if(item!=null)list.Add(item);}return list;}}
    internal T? Find<T>(string kind,string id){lock(gate){using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT json FROM objects WHERE kind=$k AND id=$i";cmd.Parameters.AddWithValue("$k",kind);cmd.Parameters.AddWithValue("$i",id);var json=cmd.ExecuteScalar() as string;return json==null?default:JsonSerializer.Deserialize<T>(json);}}
    internal List<Article> Query(string day,string source,bool discovered,bool favorites,int offset,bool unread=false){lock(gate){using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT json FROM objects WHERE kind='article' AND ($s='' OR json_extract(json,'$.SourceId')=$s) AND ($unread=0 OR json_extract(json,'$.Read')=0) AND ($fav=1 AND json_extract(json,'$.Favorite')=1 OR $fav=0 AND substr(json_extract(json,$date),1,10)=$d) ORDER BY json_extract(json,'$.Discovered') DESC LIMIT 200 OFFSET $o";cmd.Parameters.AddWithValue("$s",source);cmd.Parameters.AddWithValue("$fav",favorites?1:0);cmd.Parameters.AddWithValue("$unread",unread?1:0);cmd.Parameters.AddWithValue("$date",discovered?"$.Discovered":"$.PublishedDay");cmd.Parameters.AddWithValue("$d",day);cmd.Parameters.AddWithValue("$o",offset);using var r=cmd.ExecuteReader();var list=new List<Article>();while(r.Read())list.Add(JsonSerializer.Deserialize<Article>(r.GetString(0))!);return list;}}
    internal Article? FindUrl(string url){lock(gate){using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT json FROM objects WHERE kind='article' AND json_extract(json,'$.Url')=$u LIMIT 1";cmd.Parameters.AddWithValue("$u",url);return cmd.ExecuteScalar() is string text?JsonSerializer.Deserialize<Article>(text):null;}}
    internal List<Article> Pending(string day,int limit){lock(gate){using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT json FROM (SELECT json,ROW_NUMBER() OVER(PARTITION BY json_extract(json,'$.SourceId') ORDER BY json_extract(json,'$.Discovered')) AS turn FROM objects WHERE kind='article' AND json_extract(json,'$.PublishedDay')=$d AND json_extract(json,'$.SummaryKey')='' AND json_extract(json,'$.Status') IN ('待生成','文章已更新，摘要待更新') AND json_extract(json,'$.SourceId') IN (SELECT id FROM objects WHERE kind='source' AND json_extract(json,'$.Subscribed')=1)) ORDER BY turn LIMIT $l";cmd.Parameters.AddWithValue("$d",day);cmd.Parameters.AddWithValue("$l",Math.Clamp(limit,1,500));using var r=cmd.ExecuteReader();var list=new List<Article>();while(r.Read())list.Add(JsonSerializer.Deserialize<Article>(r.GetString(0))!);return list;}}
    internal void Put<T>(string kind,string id,T value){lock(gate){using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="INSERT INTO objects VALUES($k,$i,$j) ON CONFLICT(kind,id) DO UPDATE SET json=excluded.json";cmd.Parameters.AddWithValue("$k",kind);cmd.Parameters.AddWithValue("$i",id);cmd.Parameters.AddWithValue("$j",JsonSerializer.Serialize(value));cmd.ExecuteNonQuery();}}
    internal void Delete(string kind,string id){lock(gate){using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="DELETE FROM objects WHERE kind=$k AND id=$i";cmd.Parameters.AddWithValue("$k",kind);cmd.Parameters.AddWithValue("$i",id);cmd.ExecuteNonQuery();}}
    internal bool Reserve(string day,int limit){lock(gate){using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="INSERT INTO usage VALUES($d,1) ON CONFLICT(day) DO UPDATE SET n=n+1 WHERE n<$l RETURNING n";cmd.Parameters.AddWithValue("$d",day);cmd.Parameters.AddWithValue("$l",limit);return limit>0&&cmd.ExecuteScalar()!=null;}}
    internal void MigrateAbstractMode(){
        if(Find<int>("migration","abstract-mode")==1)return;
        lock(gate){using var c=Open();using var cmd=c.CreateCommand();
            cmd.CommandText="DELETE FROM objects WHERE kind IN ('job','failed-job'); UPDATE objects SET json=json_remove(json,'$.FullSummary','$.FullKey') WHERE kind='article'; UPDATE objects SET json=json_set(json,'$.Status',CASE WHEN length(json_extract(json,'$.Summary'))>0 THEN '基于摘要' ELSE '待生成' END) WHERE kind='article' AND (json_extract(json,'$.Status') LIKE '%精读%' OR json_extract(json,'$.Status') IN ('等待闲置','正在提取全文'));";
            cmd.ExecuteNonQuery();}
        var cache=Path.GetFullPath(Path.Combine(DirectoryPath,"cache"));
        if(System.IO.Directory.Exists(cache)&&!new DirectoryInfo(cache).Attributes.HasFlag(FileAttributes.ReparsePoint))
            foreach(var file in new DirectoryInfo(cache).GetFiles())if(!file.Attributes.HasFlag(FileAttributes.ReparsePoint))file.Delete();
        Put("migration","abstract-mode",1);
    }
    internal void Prune(){lock(gate){using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="DELETE FROM objects WHERE kind='article' AND json_extract(json,'$.Favorite')=0 AND julianday(json_extract(json,'$.Discovered'))<julianday($cut)";cmd.Parameters.AddWithValue("$cut",DateTimeOffset.Now.AddDays(-90).ToString("O"));cmd.ExecuteNonQuery();}}
}
