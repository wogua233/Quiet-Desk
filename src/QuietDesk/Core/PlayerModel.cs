using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace QuietDesk;

internal sealed class PlayerModel : Observable,IDisposable
{
    private readonly AudioEngine engine;private readonly StateStore store;private readonly SavedState saved;
    private readonly DeviceWatcher devices;private readonly InteractionFeedback feedback=new();
    private bool mediaPaused,mediaBusy;private bool sourceDirect;private string sourceFormat="MP3";private int deviceVersion;
    public bool MediaPaused=>mediaPaused;public bool HasMedia=>sourceLocation!=null;public bool CanControlMedia=>HasMedia||Loading;public bool RefreshingDevices {get;private set;}
    public string MediaPauseLabel=>Loading?"取消连接":(mediaPaused?"继续":"暂停")+(sourceOnline?"电台":"音乐");
    public string? OutputDeviceId=>saved.OutputDeviceId;
    public OutputDevice? SelectedOutputDevice=>AvailableDevices.FirstOrDefault(d=>d.Id==OutputDeviceId);
    public ObservableCollection<OutputDevice> AvailableDevices {get;}=new();
    private bool desktopTransparencyAvailable;
    public bool DesktopTransparencyAvailable {get=>desktopTransparencyAvailable;set{desktopTransparencyAvailable=value;Changed();}}
    public double DesktopOpacity {get=>saved.DesktopOpacity;set{saved.DesktopOpacity=Math.Clamp(value,.65,1);Changed();ScheduleSave();}}
    private void RefreshMedia(){Changed(nameof(MediaPaused));Changed(nameof(HasMedia));Changed(nameof(CanControlMedia));Changed(nameof(MediaPauseLabel));Changed(nameof(CurrentFile));}
    public void RefreshDevices(){RefreshingDevices=true;try{AvailableDevices.Clear();foreach(var d in OutputDevices.List())AvailableDevices.Add(d);}catch(Exception e){Status="无法枚举音频设备："+e.Message;}finally{RefreshingDevices=false;Changed(nameof(OutputDeviceId));Changed(nameof(SelectedOutputDevice));}}
    private readonly DispatcherTimer tick,saveTimer;private readonly Stopwatch stopwatch=Stopwatch.StartNew();
    private double previous;private int saveTicks,sourceVersion,retry;private bool bulk,disposed,playing,loading;
    private Scene? deletedScene;private double beforeMute=.45;
    public bool CanUndoScene=>deletedScene!=null;
    public string? CurrentFile=>sourceOnline?null:sourceLocation;
    public bool FeedbackEnabled {get=>saved.FeedbackEnabled;set{saved.FeedbackEnabled=value;Changed();ScheduleSave();}}
    public double FeedbackVolume {get=>saved.FeedbackVolume;set{saved.FeedbackVolume=Math.Clamp(value,0,1);Changed();ScheduleSave();}}
    public bool ReduceMotion {get=>saved.ReduceMotion;set{saved.ReduceMotion=value;Changed();ScheduleSave();}}
    public bool OnboardingSeen=>saved.OnboardingSeen;
    public void DismissOnboarding(){saved.OnboardingSeen=true;ScheduleSave();}
    public string MuteLabel=>Master==0?"恢复音量":"静音";
    public void ToggleMute(){if(Master>0){beforeMute=Master;Master=0;}else Master=beforeMute>0?beforeMute:.45;}
    public void Feedback(bool success=false){if(FeedbackEnabled)PlayFeedback(success);}
    public void PreviewFeedback(){if(Master<=0){Status="总音量已静音，请先恢复音量再试听。";return;}PlayFeedback(true);}
    private void PlayFeedback(bool success)=>feedback.Play(FeedbackVolume*Master,OutputDeviceId,success,message=>Application.Current.Dispatcher.BeginInvoke(()=>Status=message));
    private string status="选一种声音，给自己一点安静。",sceneName="雨天书桌",mediaTitle="未选择音乐";
    private double master,mediaVolume;private string? sourceLocation;private bool sourceOnline;private CancellationTokenSource? opening;
    public ObservableCollection<SoundChannel> Channels {get;}=new(Catalog.Sounds.Select(s=>new SoundChannel(s)));
    public ObservableCollection<Scene> Scenes {get;}=new();public ObservableCollection<Station> Favorites {get;}=new();
    public ObservableCollection<string> Queue {get;}=new();public FocusClock Focus {get;}
    public RadioDirectory Radio {get;}
    public string DataDirectory=>store.DirectoryPath;
    public string Status {get=>status;set=>Set(ref status,value);}
    public string SceneName {get=>sceneName;private set=>Set(ref sceneName,value);}
    public string MediaTitle {get=>mediaTitle;private set=>Set(ref mediaTitle,value);}
    public bool Playing {get=>playing;private set {Set(ref playing,value);Changed(nameof(PlayLabel));}}
    public bool Loading {get=>loading;private set {Set(ref loading,value);Changed(nameof(PlayLabel));Changed(nameof(MediaPauseLabel));Changed(nameof(CanControlMedia));}}
    public string PlayLabel=>Playing?"全部暂停":"全部恢复";
    public string FocusText=>TimeSpan.FromSeconds(Math.Ceiling(Focus.Remaining)).ToString(Focus.Remaining>=3600?@"h\:mm\:ss":@"mm\:ss");
    public string FocusLabel=>Focus.Running?"暂停专注":Focus.Completed?"再专注一次":Focus.Remaining<Focus.Minutes*60?"继续专注":"开始专注";
    public string FocusCaption=>Focus.Completed?"本轮完成 · 不必急着开始下一轮":Focus.Running?"专注进行中 · 静默陪伴":"静默计时 · 按自己的节奏";
    public string Today=>$"今日专注  {Math.Floor(Focus.Daily.GetValueOrDefault(DateTime.Now.ToString("yyyy-MM-dd"))/60)} 分钟";
    public double Master {get=>master;set{if(Set(ref master,Math.Clamp(value,0,1))){engine.SetMaster(master);Changed(nameof(MuteLabel));ScheduleSave();}}}
    public double MediaVolume {get=>mediaVolume;set{if(Set(ref mediaVolume,Math.Clamp(value,0,1))){engine.SetMediaVolume(mediaVolume);ScheduleSave();}}}
    public bool LoopQueue {get=>saved.LoopQueue;set{saved.LoopQueue=value;Changed();ScheduleSave();}}
    public PlayerModel(string directory)
    {
        store=new(directory);saved=store.Load();Radio=new(directory);sceneName=string.IsNullOrWhiteSpace(saved.SceneName)?"我的声音组合":saved.SceneName;
        engine=new AudioEngine(Path.Combine(AppContext.BaseDirectory,"Assets","Sounds"),saved.OutputDeviceId);
        master=saved.Master;mediaVolume=saved.MediaVolume;engine.SetMaster(master);engine.SetMediaVolume(mediaVolume);
        Focus=new(saved.FocusSeconds);Focus.Configure(saved.FocusMinutes);
        foreach(var s in saved.Scenes)Scenes.Add(s);foreach(var s in saved.Favorites)Favorites.Add(s);foreach(var p in saved.Queue)Queue.Add(p);
        foreach(var c in Channels){c.Enabled=saved.Levels.ContainsKey(c.Info.Id);c.Volume=saved.Levels.GetValueOrDefault(c.Info.Id,.5);c.PropertyChanged+=ChannelChanged;engine.SetLevel(c.Info.Id,c.Enabled?c.Volume:0);}
        tick=new DispatcherTimer(TimeSpan.FromSeconds(1),DispatcherPriority.Background,(_,_)=>Tick(),Dispatcher.CurrentDispatcher);tick.Stop();
        saveTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(650)};saveTimer.Tick+=(_,_)=>{saveTimer.Stop();Save();};
        engine.Failed+=error=>Application.Current.Dispatcher.BeginInvoke(()=>{Playing=false;Status=error;try{engine.ReopenOutput();}catch(Exception ex){Status=ex.Message;}UpdateTick();});
        devices=new DeviceWatcher(()=>Application.Current.Dispatcher.BeginInvoke(()=>{if(!disposed)DeviceChanged();}));
        SystemEvents.PowerModeChanged+=PowerChanged;
        RefreshDevices();if(OutputDeviceId!=null&&!AvailableDevices.Any(d=>d.Id==OutputDeviceId)){saved.OutputDeviceId=null;engine.SetDevice(null);Status="原输出设备不可用，已回退系统默认设备。";Changed(nameof(OutputDeviceId));Changed(nameof(SelectedOutputDevice));ScheduleSave();}if(store.Warning!=null)Status=store.Warning;
    }
    private void ChannelChanged(object? sender,PropertyChangedEventArgs e)
    {
        if(bulk)return;var channel=(SoundChannel)sender!;
        if(Channels.Count(c=>c.Enabled)>4){bulk=true;channel.Enabled=false;bulk=false;Status="最多同时开启四种环境声，请先关闭其中一种。";return;}
        engine.SetLevel(channel.Info.Id,channel.Enabled?channel.Volume:0);SceneName="我的声音组合";ScheduleSave();
    }
    public void ApplyScene(Scene scene)
    {
        bulk=true;foreach(var c in Channels){c.Enabled=false;}
        foreach(var pair in scene.Levels.Where(p=>double.IsFinite(p.Value)).Take(4)){var c=Channels.FirstOrDefault(x=>x.Info.Id==pair.Key);if(c!=null){c.Volume=pair.Value;c.Enabled=true;}}
        foreach(var c in Channels)engine.SetLevel(c.Info.Id,c.Enabled?c.Volume:0);bulk=false;SceneName=scene.Name;Status="场景已就绪，播放由你决定。";ScheduleSave();
    }
    public void SaveScene(string name)
    {
        name=name.Trim();if(name.Length is <1 or >40){Status="请填写 1–40 字的场景名称。";return;}
        var old=Scenes.FirstOrDefault(s=>s.Name==name);if(old!=null)Scenes.Remove(old);
        Scenes.Add(new Scene{Name=name,Levels=Channels.Where(c=>c.Enabled).ToDictionary(c=>c.Info.Id,c=>c.Volume)});SceneName=name;ScheduleSave();Status="声音组合已保存。";Feedback(true);
    }
    public void DeleteScene(Scene scene){deletedScene=scene;Changed(nameof(CanUndoScene));Scenes.Remove(scene);ScheduleSave();Status="组合已移除，可撤销。";}
    public void UndoScene(){if(deletedScene==null)return;var restore=deletedScene;deletedScene=null;Changed(nameof(CanUndoScene));if(!Scenes.Any(s=>s.Name==restore.Name))Scenes.Add(restore);ScheduleSave();}
    public void RenameScene(Scene scene,string name){name=name.Trim();if(name.Length is <1 or >40||Scenes.Any(s=>s!=scene&&s.Name==name)){Status="名称为空、过长或已存在。";return;}var index=Scenes.IndexOf(scene);Scenes.Remove(scene);scene.Name=name;Scenes.Insert(index,scene);ScheduleSave();}
    public void ReplaceChannel(SoundChannel old,SoundChannel next){old.Enabled=false;next.Enabled=true;}
    public async Task TogglePlay()
    {
        if(Playing){CancelSource();Loading=false;Playing=false;await engine.Pause();if(!Playing&&sourceOnline)engine.SetMedia(null);Status="全部已暂停。";UpdateTick();return;}
        if(!Channels.Any(c=>c.Enabled)&&engine.Media==null&&(sourceLocation==null||mediaPaused)){Status="先开启环境声，或继续音乐。";return;}
        try{engine.SetMediaReadEnabled(!mediaPaused);engine.SetMediaVolume(MediaVolume);engine.Play();Playing=true;Status="已恢复播放。";if(sourceOnline&&!mediaPaused&&sourceLocation!=null&&engine.Media==null)await OpenSource(sourceLocation,true,MediaTitle,false,sourceFormat,sourceDirect);}catch(Exception e){Playing=false;Status=e.Message;}UpdateTick();
    }
    public async Task ToggleMedia()
    {
        if(mediaBusy)return;
        if(Loading){CancelSource();Loading=false;engine.SetMediaVolume(MediaVolume);Status="已取消连接，环境声保持不变。";return;}
        if(sourceLocation==null)return;
        mediaBusy=true;try{
            if(!mediaPaused){mediaPaused=true;CancelSource();var version=sourceVersion;await engine.FadeMedia();if(version!=sourceVersion||disposed)return;if(sourceOnline)engine.SetMedia(null);else engine.SetMediaReadEnabled(false);Status=sourceOnline?"电台已暂停，继续时回到直播。":"音乐已暂停，保留当前位置。";}
            else{mediaPaused=false;if(!Playing){Status="媒体已就绪，点击全部恢复后播放。";return;}if(sourceOnline)await OpenSource(sourceLocation,true,MediaTitle,false,sourceFormat,sourceDirect);else{engine.SetMediaReadEnabled(true);engine.SetMediaVolume(MediaVolume);Status="音乐已继续。";}}
        }finally{mediaBusy=false;RefreshMedia();}
    }
    private void CaptureFocus(){var now=stopwatch.Elapsed.TotalSeconds;if(Focus.Running)Focus.Advance(now-previous,DateTimeOffset.Now);previous=now;}
    public void ToggleFocus(){CaptureFocus();Focus.Toggle();RefreshFocus();UpdateTick();}
    public void ResetFocus(){CaptureFocus();Focus.Reset();RefreshFocus();UpdateTick();}
    public void SetFocusMinutes(int value){CaptureFocus();Focus.Configure(value);saved.FocusMinutes=Focus.Minutes;RefreshFocus();ScheduleSave();UpdateTick();}
    private void RefreshFocus(){Changed(nameof(FocusText));Changed(nameof(FocusLabel));Changed(nameof(FocusCaption));Changed(nameof(Today));}
    private void UpdateTick(){CaptureFocus();if(Focus.Running||Playing)tick.Start();else tick.Stop();}
    private void Tick()
    {
        var wasRunning=Focus.Running;var now=stopwatch.Elapsed.TotalSeconds;Focus.Advance(now-previous,DateTimeOffset.Now);previous=now;
        if(wasRunning)RefreshFocus();
        if(++saveTicks>=15){saveTicks=0;if(wasRunning)Save();}
        if(Playing&&!mediaPaused&&!mediaBusy&&!Loading&&engine.Media?.Finished==true){var error=engine.Media.Error;engine.SetMedia(null);if(sourceOnline){if(retry<2){retry++;_ = RetryRadio(sourceVersion);}else{Status="电台已断开，重试结束；离线环境声继续播放。";MediaTitle="电台已断开";}}else{_ = NextTrack();}if(error!=null)Status="音乐流已中断："+error;}
        if(!Focus.Running&&!Playing)tick.Stop();
    }
    private async Task RetryRadio(int version){Loading=true;Status=$"电台断开，正在重连（{retry}/2）…";await Task.Delay(1500);if(version!=sourceVersion||disposed)return;await OpenSource(sourceLocation!,true,MediaTitle,false,sourceFormat,sourceDirect);}
    private void PowerChanged(object sender,PowerModeChangedEventArgs e)=>Application.Current.Dispatcher.BeginInvoke(()=>{
        previous=stopwatch.Elapsed.TotalSeconds;
        if(e.Mode==PowerModes.Suspend){Focus.Pause();RefreshFocus();Save();}
        if(e.Mode==PowerModes.Resume){Status="已从睡眠恢复，专注计时保持暂停。";if(Playing){try{engine.ReopenOutput();}catch(Exception ex){Playing=false;Status=ex.Message;}}}UpdateTick();
    });
    private void CancelSource(){sourceVersion++;opening?.Cancel();opening?.Dispose();opening=null;}
    public Task PlayStation(Station station)=>OpenSource(station.Url,true,station.Name,true,station.Format,station.DirectConnection);
    public Task PlayFile(string path)=>OpenSource(path,false,Path.GetFileNameWithoutExtension(path),true);
    private async Task OpenSource(string location,bool online,string title,bool resetRetry,string format="MP3",bool direct=false)
    {
        CancelSource();var version=sourceVersion;opening=new();var token=opening.Token;Loading=true;if(resetRetry)retry=0;
        Status="正在连接："+title;
        try {
            MediaSession? session=null;
            for(int attempt=0;attempt<(online?3:1);attempt++){
                try{session=await MediaSession.Open(location,online,token,format,direct);break;}catch(Exception)when(!token.IsCancellationRequested&&online&&attempt<2){Status=$"连接暂未成功，重试 {attempt+1}/2…";await Task.Delay(1000,token);}
            }
            if(version!=sourceVersion||disposed){session?.Dispose();return;}
            await engine.FadeMedia();
            if(version!=sourceVersion||disposed){session?.Dispose();return;}
            engine.SetMedia(session);sourceLocation=location;sourceOnline=online;sourceFormat=format;sourceDirect=direct;mediaPaused=false;MediaTitle=title;RefreshMedia();
            engine.Play();Playing=true;Status="正在播放："+title;UpdateTick();
        }catch(OperationCanceledException){if(version==sourceVersion)Status="连接已取消。";}
        catch(Exception e){if(version==sourceVersion)Status="无法播放："+e.Message;}
        finally{if(version==sourceVersion){engine.SetMediaVolume(MediaVolume);Loading=false;}}
    }
    public async void StopMedia(){CancelSource();var version=sourceVersion;Loading=false;await engine.FadeMedia();if(disposed||version!=sourceVersion)return;engine.SetMedia(null);engine.SetMediaVolume(MediaVolume);sourceLocation=null;mediaPaused=false;RefreshMedia();MediaTitle="未选择音乐";Status="已停止音乐，环境声保持不变。";}
    public Task PreviousTrack(){if(Queue.Count==0)return Task.CompletedTask;int i=sourceLocation==null?0:Queue.IndexOf(sourceLocation);return PlayFile(Queue[Math.Max(0,i-1)]);}
    public void MoveFile(string source,string target){int from=Queue.IndexOf(source),to=Queue.IndexOf(target);if(from>=0&&to>=0&&from!=to){Queue.Move(from,to);ScheduleSave();}}
    public void LocateFile(string old,string replacement){int i=Queue.IndexOf(old);if(i>=0&&File.Exists(replacement)){Queue[i]=replacement;ScheduleSave();}}
    public async Task NextTrack(){if(Queue.Count==0)return;int index=sourceLocation==null?-1:Queue.IndexOf(sourceLocation);int next=index+1;if(next>=Queue.Count){if(!LoopQueue){StopMedia();return;}next=0;}await PlayFile(Queue[next]);}
    public void AddFiles(string[] files){foreach(var f in files.Where(File.Exists))if(!Queue.Contains(f))Queue.Add(f);ScheduleSave();}
    public void RemoveFile(string file){Queue.Remove(file);ScheduleSave();}
    public void Favorite(Station station){var old=Favorites.FirstOrDefault(s=>s.Url==station.Url);if(old!=null){Favorites.Remove(old);Status="已取消收藏。";}else{Favorites.Add(station);Status="电台已收藏。";}Changed(nameof(Favorites));ScheduleSave();}
    public async void ReconnectDevice()=>await SelectDevice(OutputDeviceId);
    private void DeviceChanged(){RefreshDevices();if(OutputDeviceId!=null&&!AvailableDevices.Any(x=>x.Id==OutputDeviceId)){_ = SelectDevice(null,true);}else if(OutputDeviceId==null)_ = SelectDevice(null);}
    public async Task SelectDevice(string? id,bool fallback=false){int version=++deviceVersion;try{bool resume=Playing;await engine.Pause();if(version!=deviceVersion||disposed)return;engine.SetDevice(id);saved.OutputDeviceId=id;Changed(nameof(OutputDeviceId));Changed(nameof(SelectedOutputDevice));if(resume&&Playing)engine.Play();Status=fallback?"所选设备已移除，已回退系统默认设备。":"输出设备已更新。";ScheduleSave();}catch(Exception e){Playing=false;Status="切换输出设备失败："+e.Message;}UpdateTick();}
    private void ScheduleSave(){if(saveTimer==null)return;saveTimer.Stop();saveTimer.Start();}
    private void Save(){saved.SceneName=SceneName;saved.Master=Master;saved.MediaVolume=MediaVolume;saved.Levels=Channels.Where(c=>c.Enabled).ToDictionary(c=>c.Info.Id,c=>c.Volume);saved.Scenes=Scenes.ToList();saved.Favorites=Favorites.ToList();saved.Queue=Queue.ToList();try{store.Save(saved);}catch(Exception e)when(e is IOException or UnauthorizedAccessException){Status="保存失败："+e.Message;}}
    public void Dispose(){disposed=true;CancelSource();tick.Stop();saveTimer.Stop();SystemEvents.PowerModeChanged-=PowerChanged;Save();devices.Dispose();feedback.Dispose();Radio.Dispose();engine.Dispose();}
}
