using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace QuietDesk;

internal sealed class MainWindow : Window
{
    private readonly Reading.ReadingViewModel reading;
    private readonly FrameworkElement playerBar;
    private readonly Border contentFrame;
    internal void DisposeReading()=>reading.Dispose();
    private readonly WrapPanel savedSceneButtons=new();
    private readonly PlayerModel model;private readonly ContentControl body=new();private readonly StackPanel nav=new();
    private CancellationTokenSource? search;private int currentPage;private readonly System.Collections.Generic.Dictionary<int,UIElement> pages=new();private readonly System.Collections.Generic.Dictionary<int,double> offsets=new();
    internal Action? ReattachDesktop;internal Action? ExitApp;
    internal MainWindow(PlayerModel model)
    {
        reading=new Reading.ReadingViewModel(model.DataDirectory);
        this.model=model;Title="静隅 · Quiet Desk";Width=1100;Height=790;MinWidth=760;MinHeight=560;MaxHeight=SystemParameters.WorkArea.Height;MaxWidth=SystemParameters.WorkArea.Width;UseLayoutRounding=true;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        WindowChrome.DarkTitle(this);
        Background=Ui.B("#111111");Foreground=Ui.B("#FCFCFC");FontFamily=new FontFamily("Microsoft YaHei UI");
        var shell=new Grid{Background=Background};shell.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});shell.RowDefinitions.Add(new(){Height=GridLength.Auto});Content=shell;
        var upper=new Grid();upper.ColumnDefinitions.Add(new(){Width=new GridLength(176)});upper.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});shell.Children.Add(upper);
        var side=new DockPanel{Margin=new Thickness(20,28,16,20)};upper.Children.Add(side);
        var logo=new StackPanel();DockPanel.SetDock(logo,Dock.Top);side.Children.Add(logo);
        logo.Children.Add(Ui.Text("静 隅",28,"#FCFCFC"));logo.Children.Add(Ui.Text("Q U I E T  D E S K",9,"#95919B"));Ui.Gap(logo,40);
        var footer=new StackPanel();DockPanel.SetDock(footer,Dock.Bottom);side.Children.Add(footer);
        footer.Children.Add(Ui.Text("给注意力，一处栖息。",11,"#95919B"));Ui.Gap(footer,12);footer.Children.Add(Ui.Button("收起到桌面",()=>Hide()));
        side.Children.Add(new ScrollViewer{Content=nav,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        contentFrame=new Border{Child=body,Padding=new Thickness(28,28,28,16),Background=Ui.B("#222224"),BorderBrush=Ui.B("#333036"),BorderThickness=new Thickness(1,0,0,0)};Grid.SetColumn(contentFrame,1);upper.Children.Add(contentFrame);
        playerBar=BuildPlayer();Grid.SetRow(playerBar,1);shell.Children.Add(playerBar);
        IsVisibleChanged+=(_,_)=>{if(currentPage==4){if(!IsVisible){body.Content=null;pages.Remove(4);}else Navigate(4);}};
        Navigate(0);
        model.Scenes.CollectionChanged+=(_,_)=>{RefreshSavedSceneButtons();pages.Remove(2);if(currentPage==2)Navigate(2);};model.Favorites.CollectionChanged+=(_,_)=>{pages.Remove(2);if(currentPage==2)Navigate(2);};
        model.Queue.CollectionChanged+=(_,_)=>{pages.Remove(2);if(currentPage==2)Navigate(2);};
        Closing+=(_,e)=>{e.Cancel=true;Hide();};
        Shortcut(Key.Space,ModifierKeys.Control,()=>_ = model.TogglePlay());
        Shortcut(Key.D1,ModifierKeys.Control,()=>Navigate(0));Shortcut(Key.D2,ModifierKeys.Control,()=>Navigate(1));
        Shortcut(Key.D3,ModifierKeys.Control,()=>Navigate(2));Shortcut(Key.D4,ModifierKeys.Control,()=>Navigate(3));
        Shortcut(Key.D5,ModifierKeys.Control,()=>Navigate(4));
        Shortcut(Key.Q,ModifierKeys.Control|ModifierKeys.Shift,()=>ExitApp?.Invoke());
    }
    private void Shortcut(Key key,ModifierKeys modifiers,Action action){var command=new RoutedCommand();InputBindings.Add(new KeyBinding(command,new KeyGesture(key,modifiers)));CommandBindings.Add(new CommandBinding(command,(_,_)=>action()));}
    private Border BuildPlayer()
    {
        var all=new StackPanel();var grid=new Grid();grid.ColumnDefinitions.Add(new());grid.ColumnDefinitions.Add(new(){Width=GridLength.Auto});grid.ColumnDefinitions.Add(new(){Width=new GridLength(190)});all.Children.Add(grid);
        var labels=new StackPanel{VerticalAlignment=VerticalAlignment.Center};labels.Children.Add(Ui.Bound(model,"SceneName",14,"#FCFCFC"));var title=Ui.Bound(model,"MediaTitle",12);title.TextTrimming=TextTrimming.CharacterEllipsis;title.TextWrapping=TextWrapping.NoWrap;labels.Children.Add(title);grid.Children.Add(labels);
        var play=Ui.Button("",()=>_ = model.TogglePlay(),true);Ui.BindContent(play,model,"PlayLabel");play.MinWidth=120;play.Margin=new Thickness(12,0,20,0);Grid.SetColumn(play,1);grid.Children.Add(play);
        var volume=new StackPanel();var mute=Ui.Button("",model.ToggleMute);mute.MinHeight=24;mute.Padding=new Thickness(3);Ui.BindContent(mute,model,"MuteLabel");volume.Children.Add(mute);volume.Children.Add(Ui.Slider(model,"Master","总音量"));Grid.SetColumn(volume,2);grid.Children.Add(volume);
        var media=new WrapPanel{Margin=new Thickness(0,8,0,0)};var toggle=Ui.Button("",()=>_ = model.ToggleMedia());Ui.BindContent(toggle,model,"MediaPauseLabel");toggle.SetBinding(IsEnabledProperty,new System.Windows.Data.Binding("CanControlMedia"){Source=model});media.Children.Add(toggle);var previous=Ui.Button("上一首",()=>_ = model.PreviousTrack());var next=Ui.Button("下一首",()=>_ = model.NextTrack());previous.Margin=next.Margin=new Thickness(8,0,0,0);media.Children.Add(previous);media.Children.Add(next);void UpdateQueue(){previous.IsEnabled=next.IsEnabled=model.Queue.Count>0;}model.Queue.CollectionChanged+=(_,_)=>UpdateQueue();UpdateQueue();var mediaVolume=Ui.Slider(model,"MediaVolume","音乐与电台音量");mediaVolume.Width=175;mediaVolume.Margin=new Thickness(16,0,0,0);media.Children.Add(mediaVolume);all.Children.Add(media);
        var status=Ui.Bound(model,"Status",12,"#B7B0BC");status.Margin=new Thickness(0,7,0,0);status.TextTrimming=TextTrimming.CharacterEllipsis;status.TextWrapping=TextWrapping.NoWrap;all.Children.Add(status);return new Border{Child=all,Padding=new Thickness(24,12,24,10),Background=Ui.Gradient("#1B191E","#111111"),BorderThickness=new Thickness(0,1,0,0),BorderBrush=Ui.B("#3B343F")};
    }
    internal void Navigate(int page)
    {
        playerBar.Visibility=page==4?Visibility.Collapsed:Visibility.Visible;
        contentFrame.Padding=page==4?new Thickness(16,12,16,12):new Thickness(28,28,28,16);
        if(currentPage==4&&page!=4)pages.Remove(4);
        if(body.Content is ScrollViewer old)offsets[currentPage]=old.VerticalOffset;currentPage=page;nav.Children.Clear();var names=new[]{"声音场景","在线电台","我的收藏","设置","阅读"};
        for(int i=0;i<names.Length;i++){int index=i;var b=Ui.Button(names[i],()=>Navigate(index));var label=new StackPanel{Orientation=Orientation.Horizontal};label.Children.Add(Icons.Create(new[]{"leaves","noise","heart","settings","leaves"}[i],18));var text=Ui.Text(names[i]);text.Margin=new Thickness(10,0,0,0);label.Children.Add(text);b.Content=label;b.HorizontalContentAlignment=HorizontalAlignment.Left;b.Margin=new Thickness(0,0,0,10);if(i==page){b.Background=Ui.B("#333036");b.Foreground=Ui.B("#C2BEC8");}nav.Children.Add(b);}
        if(!pages.TryGetValue(page,out var view)){view=page switch{0=>SoundPage(),1=>RadioPage(),2=>LibraryPage(),4=>new Reading.ReadingView(reading),_=>SettingsPage()};pages[page]=view;}body.Content=view;if(view is ScrollViewer scroll)scroll.Dispatcher.BeginInvoke(()=>scroll.ScrollToVerticalOffset(offsets.GetValueOrDefault(page)));
    }
    private StackPanel Heading(string eyebrow,string title,string subtitle)
    {var p=new StackPanel();p.Children.Add(Ui.Text(eyebrow,10,"#C2BEC8"));Ui.Gap(p,8);p.Children.Add(Ui.Text(title,28,"#FCFCFC"));Ui.Gap(p,8);p.Children.Add(Ui.Text(subtitle,12));Ui.Gap(p,24);return p;}
    private void RefreshSavedSceneButtons()
    {
        savedSceneButtons.Children.Clear();
        if(model.Scenes.Count==0){savedSceneButtons.Children.Add(Ui.Text("保存当前组合后，可在这里直接选择。",12));return;}
        foreach(var scene in model.Scenes){var button=Ui.Button(scene.Name,()=>model.ApplyScene(scene));button.Content=new TextBlock{Text=scene.Name,TextTrimming=TextTrimming.CharacterEllipsis,TextWrapping=TextWrapping.NoWrap};button.MaxWidth=240;button.ToolTip=scene.Name;button.Margin=new Thickness(0);var row=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,12,8)};row.Children.Add(button);var remove=Ui.Button("删除",()=>model.DeleteScene(scene));remove.ToolTip="删除组合："+scene.Name;System.Windows.Automation.AutomationProperties.SetName(remove,"删除组合："+scene.Name);remove.Margin=new Thickness(4,0,0,0);row.Children.Add(remove);savedSceneButtons.Children.Add(row);}
    }
    private UIElement SoundPage()
    {
        var page=Heading("声音场景","给此刻，一点安静。","18 种离线声音。选好组合，让注意力慢慢安定。");
        if(!model.OnboardingSeen){var intro=new DockPanel{Margin=new Thickness(0,0,0,12)};var dismiss=Ui.Button("知道了",()=>{model.DismissOnboarding();intro.Visibility=Visibility.Collapsed;});DockPanel.SetDock(dismiss,Dock.Right);intro.Children.Add(dismiss);var hint=Ui.Text("关闭窗口后留在桌面；托盘菜单可退出。",12);hint.VerticalAlignment=VerticalAlignment.Center;intro.Children.Add(hint);page.Children.Add(intro);}
        var focus=new DockPanel();var buttons=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(buttons,Dock.Right);focus.Children.Add(buttons);var start=Ui.Button("",model.ToggleFocus,true);Ui.BindContent(start,model,"FocusLabel");buttons.Children.Add(start);var reset=Ui.Button("重置",model.ResetFocus);reset.Margin=new Thickness(8,0,0,0);buttons.Children.Add(reset);var time=new StackPanel();time.Children.Add(Ui.Bound(model,"FocusText",32,"#FCFCFC"));time.Children.Add(Ui.Bound(model,"Today",12));focus.Children.Add(time);page.Children.Add(Ui.Panel(focus));Ui.Gap(page,20);
        var save=Ui.Button("保存当前组合",()=>{var name=Ask("保存声音组合","给组合起个名字",model.SceneName);if(name!=null&&(!model.Scenes.Any(x=>x.Name==name)||Confirm("替换同名组合？","原组合将被当前音量设置替换。")))model.SaveScene(name);});save.HorizontalAlignment=HorizontalAlignment.Right;
        var presets=new WrapPanel();foreach(var scene in new[]{new Scene{Name="雨天书桌",Levels=new(){{"windowrain",.5},{"fireplace",.18}}},new Scene{Name="林间清晨",Levels=new(){{"stream",.4},{"birds",.22}}},new Scene{Name="安静阅览室",Levels=new(){{"library",.4},{"brown",.12}}},new Scene{Name="夜行列车",Levels=new(){{"train",.35},{"rain",.2}}}}){var button=Ui.Button(scene.Name,()=>model.ApplyScene(scene));button.Margin=new Thickness(0,0,8,8);presets.Children.Add(button);}var presetRow=new DockPanel{Margin=new Thickness(0,0,0,12)};DockPanel.SetDock(save,Dock.Right);presetRow.Children.Add(save);presetRow.Children.Add(presets);page.Children.Add(presetRow);
        var savedHeading=new DockPanel();var undo=Ui.Button("撤销删除",model.UndoScene);undo.SetBinding(IsEnabledProperty,new System.Windows.Data.Binding("CanUndoScene"){Source=model});undo.ToolTip="恢复最近一次删除的组合";DockPanel.SetDock(undo,Dock.Right);savedHeading.Children.Add(undo);var savedTitle=Ui.Text("我的组合",12);savedTitle.VerticalAlignment=VerticalAlignment.Center;savedHeading.Children.Add(savedTitle);page.Children.Add(savedHeading);Ui.Gap(page,8);RefreshSavedSceneButtons();page.Children.Add(savedSceneButtons);Ui.Gap(page,12);
        var query=new TextBox{ToolTip="搜索声音",Height=38,Margin=new Thickness(0,0,0,10)};System.Windows.Automation.AutomationProperties.SetName(query,"搜索离线声音");var queryWrap=new Grid();queryWrap.Children.Add(query);var placeholder=Ui.Text("搜索声音…",13,"#C2BEC8");placeholder.Margin=new Thickness(12,8,0,0);placeholder.IsHitTestVisible=false;queryWrap.Children.Add(placeholder);query.TextChanged+=(_,_)=>placeholder.Visibility=query.Text.Length==0?Visibility.Visible:Visibility.Collapsed;page.Children.Add(queryWrap);var filters=new WrapPanel();page.Children.Add(filters);var tiles=new WrapPanel();page.Children.Add(tiles);string category="全部";bool activeOnly=false;
        var filterButtons=new System.Collections.Generic.List<Button>();
        void Render(){tiles.Children.Clear();foreach(var c in model.Channels.Where(c=>(category=="全部"||c.Info.Category==category)&&(!activeOnly||c.Enabled)&&(c.Info.Name.Contains(query.Text.Trim(),StringComparison.OrdinalIgnoreCase)||c.Info.Subtitle.Contains(query.Text.Trim(),StringComparison.OrdinalIgnoreCase))))tiles.Children.Add(SoundCard(c));if(tiles.Children.Count==0)tiles.Children.Add(Ui.Text("没有匹配的声音。换个词，或查看全部。"));}
        foreach(var label in new[]{"全部","自然","空间","噪声"}){var button=Ui.Button(label,()=>{category=label;foreach(var f in filterButtons)f.Background=Ui.B((string)f.Content==label?"#514B57":"#333036");Render();});button.Margin=new Thickness(0,0,8,12);filterButtons.Add(button);filters.Children.Add(button);}var enabled=new CheckBox{Content="仅已启用",Margin=new Thickness(8,0,0,12)};enabled.Checked+=(_,_)=>{activeOnly=true;Render();};enabled.Unchecked+=(_,_)=>{activeOnly=false;Render();};filters.Children.Add(enabled);query.TextChanged+=(_,_)=>Render();Render();
        System.ComponentModel.PropertyChangedEventHandler changed=(_,e)=>{if(e.PropertyName=="Enabled"&&activeOnly)page.Dispatcher.BeginInvoke(Render);};page.Loaded+=(_,_)=>{foreach(var c in model.Channels)c.PropertyChanged+=changed;};page.Unloaded+=(_,_)=>{foreach(var c in model.Channels)c.PropertyChanged-=changed;};return new ScrollViewer{Content=page};
    }
    private UIElement SoundCard(SoundChannel c)
    {
        var stack=new StackPanel();var head=new DockPanel();var icon=Icons.Create(c.Info.Id,26,c.Info.Color);DockPanel.SetDock(icon,Dock.Right);head.Children.Add(icon);var check=new CheckBox{Content=c.Info.Name};check.SetBinding(ToggleButton.IsCheckedProperty,new System.Windows.Data.Binding("Enabled"){Source=c,Mode=System.Windows.Data.BindingMode.TwoWay});head.Children.Add(check);stack.Children.Add(head);Ui.Gap(stack,10);var sub=Ui.Text(c.Info.Subtitle,12,"#C2BEC8");sub.TextWrapping=TextWrapping.NoWrap;sub.TextTrimming=TextTrimming.CharacterEllipsis;stack.Children.Add(sub);Ui.Gap(stack,8);stack.Children.Add(Ui.Slider(c,"Volume",c.Info.Name+"音量"));
        var box=Ui.Panel(stack,new Thickness(14));box.Width=216;box.Margin=new Thickness(0,0,12,12);box.ToolTip=c.Info.Subtitle+"\n"+c.Info.Credit;
        void SelectReplacement(){var w=new Window{Owner=this,Title="替换一种声音",Width=360,Height=340,ResizeMode=ResizeMode.NoResize,Background=Background,WindowStartupLocation=WindowStartupLocation.CenterOwner};WindowChrome.DarkTitle(w);var options=new StackPanel{Margin=new Thickness(22)};options.Children.Add(Ui.Text("已开启四种。选择要替换的声音："));foreach(var old in model.Channels.Where(x=>x.Enabled)){var b=Ui.Button(old.Info.Name,()=>{model.ReplaceChannel(old,c);w.Close();});b.Margin=new Thickness(0,8,0,0);options.Children.Add(b);}options.Children.Add(Ui.Button("取消",w.Close));w.Content=options;w.ShowDialog();}
        check.PreviewMouseLeftButtonDown+=(_,e)=>{if(!c.Enabled&&model.Channels.Count(x=>x.Enabled)>=4){e.Handled=true;SelectReplacement();}};check.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Space&&!c.Enabled&&model.Channels.Count(x=>x.Enabled)>=4){e.Handled=true;SelectReplacement();}};
        void Paint(){box.BorderBrush=Ui.B(c.Enabled?"#95919B":"#514B57");box.Background=Ui.B(c.Enabled?"#333036":"#222224");}System.ComponentModel.PropertyChangedEventHandler changed=(_,e)=>{if(e.PropertyName=="Enabled")Paint();};box.Loaded+=(_,_)=>{c.PropertyChanged+=changed;Paint();};box.Unloaded+=(_,_)=>c.PropertyChanged-=changed;Paint();return box;
    }
    private UIElement RadioPage()
    {
        var page=Heading("R A D I O","远方，也可以很安静。","浏览公共电台目录；国内目录无需访问海外服务；支持 MP3 与 HLS/AAC。");
        var row=new DockPanel();var go=Ui.Button("搜索",()=>{} ,true);DockPanel.SetDock(go,Dock.Right);row.Children.Add(go);
        var query=new TextBox{ToolTip="输入电台名称",Margin=new Thickness(0,0,10,0)};row.Children.Add(query);page.Children.Add(row);Ui.Gap(page,12);
        var tags=new WrapPanel();page.Children.Add(tags);var result=new StackPanel();var notice=Ui.Text("",11,"#C2BEC8");page.Children.Add(notice);Ui.Gap(page,12);page.Children.Add(result);
        string tag="domestic";
        async Task Load(){search?.Cancel();search?.Dispose();search=new();var token=search.Token;notice.Text="正在寻找电台…";result.Children.Clear();try{var stations=await model.Radio.Search(query.Text.Trim(),tag,token);if(token.IsCancellationRequested)return;notice.Text=$"{(model.Radio.Cached?"离线缓存":tag=="domestic"?"内置国内目录":"公共目录")} · {stations.Count} 个结果 · 播放可用性以实际连接为准";foreach(var s in stations)result.Children.Add(StationRow(s));if(stations.Count==0)result.Children.Add(Ui.Text("没有匹配结果，试试其他分类或名称。"));}catch(Exception e){if(!token.IsCancellationRequested)notice.Text=e.Message;}}
        go.Click+=(_,_)=>_ = Load();query.KeyDown+=(_,e)=>{if(e.Key==Key.Enter)_ = Load();};
        foreach(var pair in new[]{("国内直连","domestic"),("氛围","ambient"),("自然","nature"),("轻音乐","relax"),("爵士","jazz"),("古典","classical"),("全部","")}){var b=Ui.Button(pair.Item1,()=>{tag=pair.Item2;_ = Load();});b.Margin=new Thickness(0,0,8,12);tags.Children.Add(b);}
        var custom=Ui.Button("＋ 添加电台直链",()=>{var url=Ask("添加电台","输入 MP3、AAC 或 M3U8 直链（不是网页）","https://");if(url==null)return;if(!RadioDirectory.IsHttp(url)){model.Status="请输入有效的 HTTP 或 HTTPS 音频地址。";return;}var name=Ask("电台名称","为这条电台命名","我的电台");if(name!=null)model.Favorite(new Station{Id=Guid.NewGuid().ToString(),Name=name,Url=url,Country="自定义",Format=url.Contains(".m3u8",StringComparison.OrdinalIgnoreCase)?"HLS":url.Contains(".aac",StringComparison.OrdinalIgnoreCase)?"AAC":"MP3",Tags="自定义"});});custom.Margin=new Thickness(0,0,0,12);tags.Children.Add(custom);
        _ = Load();return new ScrollViewer{Content=page};
    }
    private UIElement StationRow(Station station)
    {
        var row=new DockPanel();var buttons=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(buttons,Dock.Right);row.Children.Add(buttons);
        var play=Ui.Button("播放",()=>_ = model.PlayStation(station),true);buttons.Children.Add(play);var favorite=Ui.Button(model.Favorites.Any(s=>s.Url==station.Url)?"已收藏":"收藏",()=>model.Favorite(station));favorite.Margin=new Thickness(8,0,0,0);buttons.Children.Add(favorite);System.Collections.Specialized.NotifyCollectionChangedEventHandler favoriteChanged=(_,_)=>favorite.Content=model.Favorites.Any(s=>s.Url==station.Url)?"已收藏":"收藏";favorite.Loaded+=(_,_)=>model.Favorites.CollectionChanged+=favoriteChanged;favorite.Unloaded+=(_,_)=>model.Favorites.CollectionChanged-=favoriteChanged;
        var labels=new StackPanel{Margin=new Thickness(0,0,14,0),VerticalAlignment=VerticalAlignment.Center};var name=Ui.Text(station.Name,13,"#FCFCFC");name.TextWrapping=TextWrapping.NoWrap;name.TextTrimming=TextTrimming.CharacterEllipsis;name.ToolTip=station.Name;labels.Children.Add(name);var detail=Ui.Text(station.Detail,10,"#95919B");detail.TextWrapping=TextWrapping.NoWrap;detail.TextTrimming=TextTrimming.CharacterEllipsis;labels.Children.Add(detail);row.Children.Add(labels);
        var panel=Ui.Panel(row,new Thickness(14));panel.Margin=new Thickness(0,0,0,8);return panel;
    }
    private UIElement LibraryPage()
    {
        var page=Heading("Y O U R  S P A C E","收好，喜欢的声音。","声音组合、电台收藏和本地音乐，都留在这里。");
        page.Children.Add(Ui.Text("我的声音组合",16,"#C2BEC8"));Ui.Gap(page,10);
        if(model.Scenes.Count==0)page.Children.Add(Ui.Text("还没有保存的组合。在声音场景中调好混音，再点击“保存组合”。",12));
        if(model.CanUndoScene)page.Children.Add(Ui.Button("撤销删除",model.UndoScene));
        foreach(var scene in model.Scenes.ToArray()){var row=new WrapPanel();var use=Ui.Button(scene.Name,()=>model.ApplyScene(scene));row.Children.Add(use);var rename=Ui.Button("重命名",()=>{var name=Ask("重命名组合","组合名称",scene.Name);if(name!=null)model.RenameScene(scene,name);});row.Children.Add(rename);row.Children.Add(Ui.Button("更新",()=>{if(Confirm("更新这个组合？","将保存当前开启的声音和音量。"))model.SaveScene(scene.Name);}));var remove=Ui.Button("移除",()=>model.DeleteScene(scene));remove.Margin=new Thickness(8,0,0,8);row.Children.Add(remove);page.Children.Add(row);}
        Ui.Gap(page,22);page.Children.Add(Ui.Text("收藏的电台",16,"#C2BEC8"));Ui.Gap(page,10);if(model.Favorites.Count==0)page.Children.Add(Ui.Text("从在线电台中收藏，或添加你熟悉的 MP3 电台直链。"));foreach(var s in model.Favorites)page.Children.Add(StationRow(s));
        Ui.Gap(page,22);var actions=new WrapPanel();actions.Children.Add(Ui.Button("＋ 导入本地音乐",()=>{var dialog=new OpenFileDialog{Multiselect=true,Filter="音乐文件|*.mp3;*.wav;*.flac",Title="选择本地音乐"};if(dialog.ShowDialog(this)==true)model.AddFiles(dialog.FileNames);},true));var stop=Ui.Button("停止音乐",model.StopMedia);stop.Margin=new Thickness(8,0,8,0);actions.Children.Add(stop);var loop=new CheckBox{Content="循环队列",VerticalAlignment=VerticalAlignment.Center};loop.SetBinding(ToggleButton.IsCheckedProperty,new System.Windows.Data.Binding("LoopQueue"){Source=model,Mode=System.Windows.Data.BindingMode.TwoWay});actions.Children.Add(loop);page.Children.Add(actions);Ui.Gap(page,12);
        if(model.Queue.Count==0)page.Children.Add(Ui.Text("支持 MP3、WAV、FLAC；音乐可与自然声同时播放。"));
        foreach(var file in model.Queue.ToArray()){var row=new DockPanel{Margin=new Thickness(0,4,0,4)};var buttons=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(buttons,Dock.Right);row.Children.Add(buttons);buttons.Children.Add(Ui.Button("播放",()=>_ = model.PlayFile(file)));if(!System.IO.File.Exists(file)){buttons.Children.Add(Ui.Button("定位",()=>{var pick=new OpenFileDialog{Filter="音乐|*.mp3;*.wav;*.flac"};if(pick.ShowDialog(this)==true)model.LocateFile(file,pick.FileName);}));}var del=Ui.Button("移除",()=>model.RemoveFile(file));del.Margin=new Thickness(8,0,0,0);buttons.Children.Add(del);var title=Ui.Text(System.IO.Path.GetFileName(file),12);title.VerticalAlignment=VerticalAlignment.Center;title.TextWrapping=TextWrapping.NoWrap;title.TextTrimming=TextTrimming.CharacterEllipsis;title.ToolTip=file;void Mark(){title.Text=(model.CurrentFile==file?"正在播放 · ":"")+System.IO.Path.GetFileName(file)+(System.IO.File.Exists(file)?"":" · 文件缺失");title.Foreground=Ui.B(model.CurrentFile==file?"#FCFCFC":"#C2BEC8");}System.ComponentModel.PropertyChangedEventHandler mark=(_,e)=>{if(e.PropertyName=="CurrentFile")Mark();};row.Loaded+=(_,_)=>{model.PropertyChanged+=mark;Mark();};row.Unloaded+=(_,_)=>model.PropertyChanged-=mark;row.AllowDrop=true;Point dragStart=new();title.PreviewMouseLeftButtonDown+=(_,e)=>dragStart=e.GetPosition(title);title.MouseMove+=(_,e)=>{if(e.LeftButton==MouseButtonState.Pressed&&(e.GetPosition(title)-dragStart).Length>SystemParameters.MinimumHorizontalDragDistance)DragDrop.DoDragDrop(title,new DataObject("QuietDesk.Queue",file),DragDropEffects.Move);};row.Drop+=(_,e)=>{if(e.Data.GetData("QuietDesk.Queue") is string source)model.MoveFile(source,file);};row.Children.Add(title);page.Children.Add(row);}
        return new ScrollViewer{Content=page};
    }
    private UIElement SettingsPage()
    {
        var page=Heading("P R E F E R E N C E S","按你的节奏。","保持简单，只调整真正影响体验的选项。");
        var form=new StackPanel();form.Children.Add(Ui.Text("每轮专注时长",15,"#FCFCFC"));Ui.Gap(form,8);
        var line=new StackPanel{Orientation=Orientation.Horizontal};var minutes=new TextBox{Text=model.Focus.Minutes.ToString(),Width=75,Margin=new Thickness(0,0,10,0)};line.Children.Add(minutes);line.Children.Add(Ui.Button("应用（1–240 分钟）",()=>{if(int.TryParse(minutes.Text,out var n)&&n>=1&&n<=240){if(model.Focus.Running&&!Confirm("重置正在进行的专注？","修改时长后，本轮计时将重新开始。"))return;model.SetFocusMinutes(n);model.Status="专注时长已更新，计时已重置。";}else model.Status="请输入 1–240 之间的整数分钟。";}));form.Children.Add(line);Ui.Gap(form,24);
        form.Children.Add(Ui.Text("音乐 / 电台音量",15,"#FCFCFC"));var slider=Ui.Slider(model,"MediaVolume","音乐与电台音量");slider.MaxWidth=340;slider.HorizontalAlignment=HorizontalAlignment.Left;slider.Width=340;form.Children.Add(slider);Ui.Gap(form,20);
        var feedback=new CheckBox{Content="轻音效（默认关闭）"};feedback.SetBinding(ToggleButton.IsCheckedProperty,new System.Windows.Data.Binding("FeedbackEnabled"){Source=model,Mode=System.Windows.Data.BindingMode.TwoWay});form.Children.Add(feedback);form.Children.Add(Ui.Slider(model,"FeedbackVolume","轻音效音量"));form.Children.Add(Ui.Button("试听轻音效",model.PreviewFeedback,feedback:false));var motion=new CheckBox{Content="减少动态效果"};motion.SetBinding(ToggleButton.IsCheckedProperty,new System.Windows.Data.Binding("ReduceMotion"){Source=model,Mode=System.Windows.Data.BindingMode.TwoWay});form.Children.Add(motion);Ui.Gap(form,18);
        form.Children.Add(Ui.Text("声音输出设备",15,"#FCFCFC"));Ui.Gap(form,8);var output=new ComboBox{ItemsSource=model.AvailableDevices,DisplayMemberPath="Name",SelectedValuePath="Id",MaxWidth=480,HorizontalAlignment=HorizontalAlignment.Stretch};output.SetBinding(Selector.SelectedItemProperty,new System.Windows.Data.Binding("SelectedOutputDevice"){Source=model,Mode=System.Windows.Data.BindingMode.OneWay});output.SelectionChanged+=(_,_)=>{if(!model.RefreshingDevices&&output.SelectedItem is OutputDevice device&&device.Id!=model.OutputDeviceId)_ = model.SelectDevice(device.Id);};form.Children.Add(output);Ui.Gap(form,8);form.Children.Add(Ui.Button("刷新设备列表",model.RefreshDevices));Ui.Gap(form,18);form.Children.Add(Ui.Text("桌面背景不透明度",15,"#FCFCFC"));var opacity=Ui.Slider(model,"DesktopOpacity","桌面背景不透明度");opacity.SetBinding(UIElement.IsEnabledProperty,new System.Windows.Data.Binding("DesktopTransparencyAvailable"){Source=model});form.Children.Add(opacity);form.Children.Add(Ui.Text(model.DesktopTransparencyAvailable?"仅调整背景，文字保持清晰。":"此桌面宿主暂不支持透明，使用不透明渐变。",12));Ui.Gap(form,18);
        form.Children.Add(Ui.Button("重新连接所选设备",model.ReconnectDevice));Ui.Gap(form,10);form.Children.Add(Ui.Button("重新嵌入桌面组件",()=>ReattachDesktop?.Invoke()));Ui.Gap(form,24);
        form.Children.Add(Ui.Text("安静的默认设置",15,"#FCFCFC"));Ui.Gap(form,10);form.Children.Add(Ui.Text("启动不自动播放，不随系统开机启动。\n专注结束不响铃、不弹窗；系统睡眠时暂停专注。\n所有收藏、场景和统计保存在此设备。",12));Ui.Gap(form,18);
        form.Children.Add(Ui.Text("音频问题诊断",15,"#FCFCFC"));Ui.Gap(form,8);form.Children.Add(Ui.Text("播放问题声音时导出 5 秒软件输出。只保存静隅自身的音频与音量设置，不录麦克风或其他应用。",12));Ui.Gap(form,8);form.Children.Add(Ui.Button("导出 5 秒音频诊断",async()=>{var path=await model.ExportAudioDiagnostic();if(path!=null){try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe","/select,\""+path+"\""){UseShellExecute=true});}catch(Exception){}}},feedback:false));Ui.Gap(form,18);
        form.Children.Add(Ui.Button("声音来源与许可",()=>ShowLicense()));Ui.Gap(form,10);form.Children.Add(Ui.Text("静隅 0.5.2 · Windows x64\nHLS/AAC 使用 FFmpeg（LGPLv3+），源码与许可见 docs。\n"+model.DataDirectory,10,"#95919B"));Ui.Gap(form,20);form.Children.Add(Ui.Button("退出静隅",()=>ExitApp?.Invoke()));page.Children.Add(Ui.Panel(form));return new ScrollViewer{Content=page};
    }
    private void ShowLicense(){var text=System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory,"SOUND-LICENSES.md"));var w=new Window{Owner=this,Title="声音来源与许可",Width=800,Height=600,Background=Background,WindowStartupLocation=WindowStartupLocation.CenterOwner};w.Content=new TextBox{Text=text,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(18)};w.ShowDialog();}
    private bool Confirm(string title,string message){var w=new Window{Owner=this,Title=title,Width=420,Height=205,ResizeMode=ResizeMode.NoResize,Background=Background,WindowStartupLocation=WindowStartupLocation.CenterOwner};WindowChrome.DarkTitle(w);var p=new StackPanel{Margin=new Thickness(22)};p.Children.Add(Ui.Text(message));Ui.Gap(p,18);var row=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};var cancel=Ui.Button("取消",()=>w.DialogResult=false);cancel.IsCancel=true;row.Children.Add(cancel);var ok=Ui.Button("确认",()=>w.DialogResult=true,true);ok.Margin=new Thickness(8,0,0,0);row.Children.Add(ok);p.Children.Add(row);w.Content=p;return w.ShowDialog()==true;}
    private string? Ask(string title,string instruction,string initial)
    {
        var w=new Window{Owner=this,Title=title,Width=430,Height=235,ResizeMode=ResizeMode.NoResize,Background=Ui.B("#222224"),WindowStartupLocation=WindowStartupLocation.CenterOwner};var p=new StackPanel{Margin=new Thickness(22)};p.Children.Add(Ui.Text(instruction));Ui.Gap(p,12);var input=new TextBox{Text=initial,MaxLength=2048};p.Children.Add(input);Ui.Gap(p,14);var ok=Ui.Button("保存",()=>{if(!string.IsNullOrWhiteSpace(input.Text))w.DialogResult=true;},true);ok.IsDefault=true;p.Children.Add(ok);var cancel=Ui.Button("取消",()=>w.DialogResult=false);cancel.IsCancel=true;p.Children.Add(cancel);w.Height=280;WindowChrome.DarkTitle(w);w.Content=p;w.Loaded+=(_,_)=>{input.Focus();input.SelectAll();};return w.ShowDialog()==true?input.Text.Trim():null;
    }
}
