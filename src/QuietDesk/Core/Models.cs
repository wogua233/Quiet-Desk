using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace QuietDesk;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Changed([CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new(name));
    protected bool Set<T>(ref T field,T value,[CallerMemberName]string? name=null) {if(EqualityComparer<T>.Default.Equals(field,value))return false;field=value;Changed(name);return true;}
}
public sealed record SoundInfo(string Id,string Name,string Subtitle,string Glyph,string Color,string Category="自然",string Credit="详见声音来源与许可");
public static class Catalog
{
    public static readonly SoundInfo[] Sounds = {
        new("rain","细雨","让纷杂，慢慢安静","☂","#9BBBC6"),new("stream","溪流","山间流动的清凉","≈","#8ABAAF"),
        new("waves","海浪","随潮汐放慢呼吸","∿","#90AECB"),new("wind","微风","穿过树梢的风","≋","#B3BFA7"),
        new("birds","森林","林间鸟鸣，清晨时分","birds","#9AB68E"),new("fireplace","炉火","一点暖意，陪伴长夜","fireplace","#D0AA88"),
        new("windowrain","窗边雨","隔着窗，听雨落下","rain","#9BBBC6"),new("storm","远雷","低沉而柔和的雨幕","storm","#A6B0CB"),
        new("night","夏夜虫鸣","夜色中的细小回声","night","#B5BE94"),new("leaves","树叶沙沙","风经过一片树冠","leaves","#9AB68E"),
        new("white","白噪声","均匀细密的背景声","noise","#AFBBB6","噪声"),new("pink","粉红噪声","柔和、平衡的频率","noise","#C1AAA9","噪声"),new("brown","棕噪声","温厚低沉的声底","noise","#B8A78E","噪声"),
        new("cafe","咖啡馆","远处人声与杯盏","cafe","#C2AE91","空间"),new("library","图书馆","安静阅览室的底声","library","#AEB8A0","空间","lwdickens · CC BY-NC 4.0，仅非商业使用"),
        new("train","列车","沿着轨道缓缓前行","train","#A0B5BD","空间"),new("fan","风扇","稳定轻柔的送风声","fan","#A3BBB5","空间"),new("keyboard","轻键盘声","有人陪伴着工作","keyboard","#B7B0C1","空间")};
}
public sealed class SoundChannel : Observable
{
    public SoundInfo Info {get;}
    private bool enabled; private double volume=.5;
    public bool Enabled {get=>enabled;set=>Set(ref enabled,value);}
    public double Volume {get=>volume;set=>Set(ref volume,Math.Clamp(double.IsFinite(value)?value:.5,0,1));}
    public SoundChannel(SoundInfo info)=>Info=info;
}
public sealed class Scene {public string Name {get;set;}=""; public Dictionary<string,double> Levels {get;set;}=new();}
public sealed class Station
{
    public string Id {get;set;}="";public string Name {get;set;}="";public string Url {get;set;}="";
    public string Tags {get;set;}="";public string Country {get;set;}="";public int Bitrate {get;set;}
    public string Format {get;set;}="MP3";public string SourcePage {get;set;}="";public string VerificationNote {get;set;}="";public bool DirectConnection {get;set;}
    public string Detail=>$"{Country}  ·  {(Bitrate>0?Bitrate+" kbps":Format)}  ·  {Tags}  {VerificationNote}";
}
public sealed class SavedState
{
    public string SceneName {get;set;}="雨天书桌";
    public int Version {get;set;}=3;
    public string? OutputDeviceId {get;set;}
    public double DesktopOpacity {get;set;}=.88;public double Master {get;set;}=.45;public double MediaVolume {get;set;}=.6;
    public int FeedbackDefaultsVersion {get;set;}
    public bool FeedbackEnabled {get;set;}=true;public double FeedbackVolume {get;set;}=.2;public bool ReduceMotion {get;set;}=false;public bool OnboardingSeen {get;set;}=false;
    public int FocusMinutes {get;set;}=25;public bool LoopQueue {get;set;}=true;
    public Dictionary<string,double> Levels {get;set;}=new(){{"rain",.5}};
    public List<Scene> Scenes {get;set;}=new();public List<Station> Favorites {get;set;}=new();
    public Dictionary<string,double> FocusSeconds {get;set;}=new();public List<string> Queue {get;set;}=new();
}
public sealed class StateStore
{
    public string DirectoryPath {get;}
    public string? Warning {get;private set;}
    public StateStore(string path) {DirectoryPath=path;Directory.CreateDirectory(path);}
    public SavedState Load()
    {
        var file=Path.Combine(DirectoryPath,"state.json"); if(!File.Exists(file))return new(){FeedbackDefaultsVersion=1};
        try {
            var s=JsonSerializer.Deserialize<SavedState>(File.ReadAllText(file))??throw new JsonException();
            if(s.FeedbackDefaultsVersion<1){s.FeedbackEnabled=true;s.FeedbackDefaultsVersion=1;}
            s.Master=double.IsFinite(s.Master)?Math.Clamp(s.Master,0,1):.45;s.MediaVolume=double.IsFinite(s.MediaVolume)?Math.Clamp(s.MediaVolume,0,1):.6;
            s.FocusMinutes=Math.Clamp(s.FocusMinutes,1,240);
            s.Levels=(s.Levels??new()).Where(p=>Catalog.Sounds.Any(i=>i.Id==p.Key)&&double.IsFinite(p.Value)).Take(4).ToDictionary(p=>p.Key,p=>Math.Clamp(p.Value,0,1));
            s.Scenes=(s.Scenes??new()).Where(x=>x is not null && !string.IsNullOrWhiteSpace(x.Name)&&x.Levels is not null).Take(100).ToList();
            s.Favorites=(s.Favorites??new()).Where(x=>x is not null && RadioDirectory.IsHttp(x.Url)).Take(500).ToList();
            s.Queue=(s.Queue??new()).Where(x=>!string.IsNullOrWhiteSpace(x)).Take(2000).ToList();
            s.FocusSeconds=(s.FocusSeconds??new()).Where(p=>double.IsFinite(p.Value)&&p.Value>=0).ToDictionary(p=>p.Key,p=>p.Value);
            return s;
        } catch(Exception e) when(e is IOException or JsonException or NotSupportedException) {
            Warning="设置文件无法读取，已使用默认设置；原文件已保留。";
            File.Copy(file,file+".invalid-"+DateTime.Now.ToString("yyyyMMddHHmmss"),true); return new();
        }
    }
    public void Save(SavedState state)
    {
        var file=Path.Combine(DirectoryPath,"state.json");var tmp=file+".tmp";
        File.WriteAllText(tmp,JsonSerializer.Serialize(state,new JsonSerializerOptions{WriteIndented=true}));File.Move(tmp,file,true);
    }
}
