using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Input;
using System.Threading.Tasks;
namespace QuietDesk.Reading;

internal sealed class ReadingViewModel:IDisposable
{
    internal DateTime SelectedDay {get;set;}=DateTime.Today;internal int SelectedPage {get;set;}
    internal string SourceId=ReadingCatalog.SubscribedFilter,SelectedId="";internal bool SortByJournal;internal bool NewestFirst=true;internal bool Discovered,Favorites,Unread;internal bool Recent=true;internal int Offset;internal double ScrollOffset;
    internal string Directory {get;}internal ReadingSettings Settings {get;private set;}internal ReadingService? Service {get;private set;}internal string Error {get;private set;}="";
    internal ReadingViewModel(string directory){Directory=directory;Settings=ReadingService.LoadSettings(directory);if(Settings.Enabled)try{Service=new(System.IO.Path.Combine(directory,"reading"),Settings);}catch(Exception e){Error="阅读数据暂不可用："+e.Message;}}
    internal void Enable(){try{Service??=new(System.IO.Path.Combine(Directory,"reading"),Settings);Settings.Enabled=true;ReadingService.SaveSettings(Directory,Settings);Error="";}catch(Exception e){Error="无法启用阅读："+e.Message;}}
    internal void Disable(){Settings.Enabled=false;ReadingService.SaveSettings(Directory,Settings);Service?.Dispose();Service=null;}
    public void Dispose()=>Service?.Dispose();
}

internal sealed class ReadingView:UserControl
{
    private readonly ReadingViewModel model;
    private readonly DockPanel shell=new();
    private readonly ContentControl body=new();
    private readonly TextBlock status=Ui.Text("",12);
    private readonly System.Collections.Generic.Dictionary<int,Button> tabs=new();
    private int page;
    private bool loaded,refreshing,narrowDetail;
    private ListBox? list;
    private Grid? split;
    private FrameworkElement? listPane;
    private ContentControl? reader;
    private TextBlock? empty;private TextBlock? rangeLabel;
    private StackPanel? sourcesPanel;
    private string sourceSearch="";
    private Article? selected;private string detailKey="",detailId="";
    private readonly System.Collections.Generic.HashSet<string> generating=new();

    internal ReadingView(ReadingViewModel model)
    {
        this.model=model;page=model.SelectedPage==1?1:0;
        FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=14;
        Background=Ui.B("#19191C");Content=shell;
        var header=new DockPanel{Margin=new Thickness(0,0,0,16)};
        var settings=Ui.Button("阅读设置",()=>Show(3),feedback:false);
        settings.Content=Icons.Create("settings",17,"#C2BEC8");
        settings.ToolTip="阅读设置";DockPanel.SetDock(settings,Dock.Right);header.Children.Add(settings);
        var navigation=new StackPanel{Orientation=Orientation.Horizontal};
        foreach(var pair in new[]{("文章",0),("订阅",1)})
        {
            var button=Ui.Button(pair.Item1,()=>Show(pair.Item2),feedback:false);
            button.Margin=new Thickness(0,0,8,0);tabs[pair.Item2]=button;navigation.Children.Add(button);
        }
        header.Children.Add(navigation);DockPanel.SetDock(header,Dock.Top);shell.Children.Add(header);
        status.Margin=new Thickness(4,10,4,0);DockPanel.SetDock(status,Dock.Bottom);shell.Children.Add(status);shell.Children.Add(body);
        Loaded+=(_,_)=>{loaded=true;if(model.Service!=null)model.Service.Updated+=Update;Show(page);};
        Unloaded+=(_,_)=>{SavePosition();loaded=false;if(model.Service!=null)model.Service.Updated-=Update;if(list!=null)list.ItemsSource=null;body.Content=null;selected=null;};
        SizeChanged+=(_,_)=>Adapt();
    }
    private void Update()
    {
        if(!loaded)return;
        Dispatcher.BeginInvoke(()=>{if(!loaded)return;status.Text=model.Service?.Status??"";
            if(page==0)RefreshList();else if(page==1)RefreshSources();});
    }
    private StackPanel Stack()=>new(){Margin=new Thickness(0,4,0,8)};
    private void SavePosition(){var scroll=Child<ScrollViewer>(list);if(scroll!=null)model.ScrollOffset=scroll.VerticalOffset;}
    private static T? Child<T>(DependencyObject? parent)where T:DependencyObject
    {
        if(parent==null)return null;for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);if(child is T t)return t;var nested=Child<T>(child);if(nested!=null)return nested;}return null;
    }
    private void Show(int target)
    {
        SavePosition();page=target;model.SelectedPage=target;list=null;split=null;reader=null;sourcesPanel=null;selected=null;detailKey="";
        foreach(var tab in tabs){tab.Value.BorderBrush=Ui.B(tab.Key==page?"#F077AF":"#403B43");tab.Value.Foreground=Ui.B(tab.Key==page?"#F077AF":"#FCFCFC");}
        if(model.Service==null)
        {
            var welcome=Stack();welcome.Children.Add(Ui.Text("留一点时间，读值得读的内容。",24,"#FCFCFC"));Ui.Gap(welcome);
            welcome.Children.Add(Ui.Text("选择期刊，阅读原始摘要与中文总结。不会自动订阅或播放通知声。"));
            Ui.Gap(welcome);welcome.Children.Add(Ui.Button("启用阅读并选择刊物",()=>{model.Enable();if(model.Service!=null)model.Service.Updated+=Update;Show(1);},feedback:false));
            welcome.Children.Add(Ui.Text(model.Error,12));body.Content=welcome;return;
        }
        body.Content=target switch{1=>Subscriptions(),3=>Settings(),_=>Articles()};status.Text=model.Service.Status;
    }
    private Button ActionButton(string title,Action action){var b=Ui.Button(title,action,feedback:false);b.Margin=new Thickness(0,0,8,8);b.VerticalAlignment=VerticalAlignment.Top;return b;}
    private CheckBox Toggle(string label,bool value,Action<bool> change)
    {
        var check=new CheckBox{Content=label,IsChecked=value,Margin=new Thickness(0,7,14,8)};
        check.Click+=(_,_)=>{change(check.IsChecked==true);model.Offset=0;model.SelectedId="";selected=null;model.ScrollOffset=0;RefreshList();};return check;
    }
    private UIElement Articles()
    {
        var outer=new DockPanel();
        var filters=new WrapPanel{Margin=new Thickness(0,0,0,8)};
        var range=new ComboBox{Width=124,ItemsSource=new[]{"最近7天","指定日期"},SelectedIndex=model.Recent?0:1,Margin=new Thickness(0,0,8,8)};
        range.SelectionChanged+=(_,_)=>{model.Recent=range.SelectedIndex==0;ResetFilter();};filters.Children.Add(range);
        var previous=ActionButton("前一天",()=>ChangeDay(-1));previous.ToolTip="查看前一天";filters.Children.Add(previous);
        var day=Input(model.SelectedDay.ToString("yyyy-MM-dd"));day.Width=142;day.Height=42;day.VerticalAlignment=VerticalAlignment.Top;day.Margin=new Thickness(0,0,8,8);day.ToolTip="日期（yyyy-MM-dd），回车确认";
        day.KeyDown+=(_,e)=>{if(e.Key==Key.Enter){if(DateTime.TryParseExact(day.Text,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var parsed)){model.SelectedDay=parsed;model.Recent=false;ResetFilter();}else status.Text="请按 yyyy-MM-dd 输入日期。";}};
        filters.Children.Add(day);
        filters.Children.Add(ActionButton("后一天",()=>ChangeDay(1)));
        filters.Children.Add(ActionButton("今天",()=>{model.SelectedDay=DateTime.Today;model.Recent=false;ResetFilter();}));
        var source=new ComboBox{Width=180,ItemsSource=new[]{new Source{Id=ReadingCatalog.SubscribedFilter,Name="订阅刊物"},new Source{Id="",Name="全部刊物"}}.Concat(model.Service!.Sources()).ToList(),SelectedValuePath="Id",SelectedValue=model.SourceId,Margin=new Thickness(0,0,8,8)};
        source.SelectionChanged+=(_,_)=>{model.SourceId=(source.SelectedItem as Source)?.Id??"";model.Offset=0;model.SelectedId="";selected=null;model.ScrollOffset=0;RefreshList();};filters.Children.Add(source);
        var mode=new ComboBox{Width=140,ItemsSource=new[]{"按发表日期","按发现日期"},SelectedIndex=model.Discovered?1:0,Margin=new Thickness(0,0,8,8),ToolTip="今天：今日发表／今日发现；往日：对应日期的发表／发现记录"};
        mode.SelectionChanged+=(_,_)=>{model.Discovered=mode.SelectedIndex==1;model.Offset=0;model.SelectedId="";selected=null;RefreshList();};filters.Children.Add(mode);
        var order=new ComboBox{Width=118,ItemsSource=new[]{"按时间排序","按期刊名排序"},SelectedIndex=model.SortByJournal?1:0,Margin=new Thickness(0,0,8,8)};
        var direction=new ComboBox{Width=136,ItemsSource=new[]{"更新的在前","更老的在前"},SelectedIndex=model.NewestFirst?0:1,IsEnabled=!model.SortByJournal,Margin=new Thickness(0,0,8,8)};
        System.Windows.Automation.AutomationProperties.SetName(order,"文章排序");
        System.Windows.Automation.AutomationProperties.SetName(direction,"时间顺序");
        order.ToolTip="期刊名按 A–Z 排列，同一期刊内按时间从新到旧";
        direction.ToolTip="时间跟随发表／发现日期选项；日期未知的文章排在最后";
        void Resort(){model.Offset=0;model.ScrollOffset=0;model.SelectedId="";selected=null;RefreshList();}
        order.SelectionChanged+=(_,_)=>{model.SortByJournal=order.SelectedIndex==1;direction.IsEnabled=!model.SortByJournal;Resort();};
        direction.SelectionChanged+=(_,_)=>{model.NewestFirst=direction.SelectedIndex==0;Resort();};
        filters.Children.Add(order);filters.Children.Add(direction);
        filters.Children.Add(Toggle("收藏（全部日期）",model.Favorites,v=>model.Favorites=v));filters.Children.Add(Toggle("仅未读",model.Unread,v=>model.Unread=v));
        filters.Children.Add(ActionButton("刷新订阅",async()=>{status.Text="正在检查订阅…";await model.Service.Refresh();}));
        DockPanel.SetDock(filters,Dock.Top);outer.Children.Add(filters);
        rangeLabel=Ui.Text("",12);rangeLabel.Margin=new Thickness(0,0,0,10);DockPanel.SetDock(rangeLabel,Dock.Top);outer.Children.Add(rangeLabel);

        split=new Grid();split.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(320)});split.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        var left=new DockPanel{Margin=new Thickness(0,0,12,0)};
        var footer=new WrapPanel();
        footer.Children.Add(ActionButton("上一页",()=>{model.Offset=Math.Max(0,model.Offset-200);selected=null;model.SelectedId="";model.ScrollOffset=0;RefreshList();}));
        footer.Children.Add(ActionButton("下一页",()=>{if(list?.Items.Count==200){model.Offset+=200;selected=null;model.SelectedId="";model.ScrollOffset=0;RefreshList();}}));
        DockPanel.SetDock(footer,Dock.Bottom);left.Children.Add(footer);
        var listArea=new Grid();
        list=new ListBox{Background=Brushes.Transparent,BorderThickness=new Thickness(0),Foreground=Ui.B("#FCFCFC"),HorizontalContentAlignment=HorizontalAlignment.Stretch};
        VirtualizingPanel.SetIsVirtualizing(list,true);VirtualizingPanel.SetVirtualizationMode(list,VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(list,true);ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);
        var itemStyle=new Style(typeof(ListBoxItem));itemStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));
        itemStyle.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(12)));itemStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(0,0,0,6)));
        var template=new ControlTemplate(typeof(ListBoxItem));var border=new FrameworkElementFactory(typeof(Border));border.Name="Card";
        border.SetValue(Border.CornerRadiusProperty,new CornerRadius(8));border.SetValue(Border.BackgroundProperty,Ui.B("#252529"));border.SetValue(Border.BorderThicknessProperty,new Thickness(1));border.SetValue(Border.BorderBrushProperty,Ui.B("#353239"));border.SetValue(Border.PaddingProperty,new Thickness(12));
        var presenter=new FrameworkElementFactory(typeof(ContentPresenter));border.AppendChild(presenter);template.VisualTree=border;
        foreach(var property in new[]{ListBoxItem.IsSelectedProperty,UIElement.IsKeyboardFocusWithinProperty,UIElement.IsMouseOverProperty})
        {
            var trigger=new Trigger{Property=property,Value=true};trigger.Setters.Add(new Setter(Border.BorderBrushProperty,Ui.B(property==UIElement.IsMouseOverProperty?"#75606C":"#F077AF"),"Card"));template.Triggers.Add(trigger);
        }
        itemStyle.Setters.Add(new Setter(Control.TemplateProperty,template));list.ItemContainerStyle=itemStyle;
        var stack=new FrameworkElementFactory(typeof(StackPanel));
        foreach(var pair in new[]{("DisplayTitle",14.0,"#FCFCFC"),("DisplayMeta",12.0,"#ADA8B1"),("Status",12.0,"#DBA5BE")})
        {
            var text=new FrameworkElementFactory(typeof(TextBlock));text.SetBinding(TextBlock.TextProperty,new Binding(pair.Item1));text.SetValue(TextBlock.TextWrappingProperty,TextWrapping.Wrap);
            text.SetValue(TextBlock.FontSizeProperty,pair.Item2);text.SetValue(TextBlock.ForegroundProperty,Ui.B(pair.Item3));text.SetValue(FrameworkElement.MarginProperty,new Thickness(0,0,0,6));stack.AppendChild(text);
        }
        list.ItemTemplate=new DataTemplate{VisualTree=stack};System.Windows.Automation.AutomationProperties.SetName(list,"文章列表");
        list.SelectionChanged+=(_,_)=>{if(!refreshing&&list.SelectedItem is Article a)Select(a,true);};
        listArea.Children.Add(list);empty=Ui.Text("",14);empty.Margin=new Thickness(16);listArea.Children.Add(empty);left.Children.Add(listArea);
        listPane=left;split.Children.Add(left);reader=new ContentControl();Grid.SetColumn(reader,1);split.Children.Add(reader);
        outer.Children.Add(split);RefreshList();Adapt();return outer;
    }
    private void ChangeDay(int amount){model.SelectedDay=model.SelectedDay.AddDays(amount);model.Recent=false;ResetFilter();}
    private void ResetFilter(){model.Offset=0;model.SelectedId="";model.ScrollOffset=0;Show(0);}
    private void RefreshList()
    {
        if(list==null||model.Service==null||page!=0)return;
        var id=selected?.Id??model.SelectedId;var scroll=Child<ScrollViewer>(list);double offset=scroll?.VerticalOffset??model.ScrollOffset;
        var articles=model.Service.Query(model.SelectedDay.ToString("yyyy-MM-dd"),model.SourceId,model.Discovered,model.Favorites,model.Offset,model.Unread,model.Recent,model.SortByJournal?ArticleOrder.Journal:model.NewestFirst?ArticleOrder.Newest:ArticleOrder.Oldest);
        // Keep the actively read item visible until the user changes a filter.
        if(model.Unread&&selected!=null&&(model.SourceId!=ReadingCatalog.SubscribedFilter||model.Service.Sources().Any(s=>s.Id==selected.SourceId&&s.Subscribed))&&selected.Id==model.SelectedId&&!articles.Any(a=>a.Id==selected.Id))articles.Insert(0,selected);
        refreshing=true;list.ItemsSource=articles;var match=articles.FirstOrDefault(a=>a.Id==id);list.SelectedItem=match;refreshing=false;
        var subscribed=model.Service.Sources().Where(s=>s.Subscribed).ToList();
        var relevant=subscribed.Where(s=>model.SourceId.Length==0||model.SourceId==ReadingCatalog.SubscribedFilter||s.Id==model.SourceId).ToList();
        if(rangeLabel!=null)rangeLabel.Text=model.Favorites?"全部日期的收藏":(model.Recent?$"{model.SelectedDay.AddDays(-6):yyyy-MM-dd} 至 {model.SelectedDay:yyyy-MM-dd}":$"{model.SelectedDay:yyyy-MM-dd}")+" · "+(model.Discovered?"按发现日期":"按发表日期")+" · 本页 "+articles.Count+" 篇";
        string latest=articles.Count==0?model.Service.LatestDay(model.SourceId):"";
        empty!.Text=articles.Count>0?"":relevant.Count==0?"尚未订阅所选刊物。\n请在“订阅”中勾选。":relevant.Any(s=>s.Failures>0)?"当前筛选没有条目，来源更新失败。\n请在“订阅”查看原因。":latest.Length>0?$"当前日期或筛选没有文章。\n最近收录的发表日期：{latest}\n请切换“最近7天”或输入该日期。":"尚未收录文章。\n刷新订阅后查看连接状态；无更新不代表获取失败。";
        empty.Visibility=articles.Count==0?Visibility.Visible:Visibility.Collapsed;
        if(match!=null)Select(match,false);else{selected=null;detailKey="";if(reader!=null)reader.Content=Ui.Panel(Ui.Text("选择一篇文章开始阅读。\n中文总结仅依据摘要或导读。",16));}
        var currentList=list;Dispatcher.BeginInvoke(()=>{if(list==currentList)Child<ScrollViewer>(currentList)?.ScrollToVerticalOffset(offset);});
    }
    private void Select(Article article,bool user)
    {
        selected=article;model.SelectedId=article.Id;if(user){narrowDetail=true;if(!article.Read){article.Read=true;model.Service!.SaveArticle(article);}if(!article.HasAbstract&&article.AbstractStatus.Length==0)_=model.Service!.LoadAbstract(article);}
        RenderDetail(article);Adapt();
    }
    private void Adapt()
    {
        if(split==null||reader==null||listPane==null)return;bool narrow=ActualWidth<720;
        split.ColumnDefinitions[0].Width=narrow?new GridLength(1,GridUnitType.Star):new GridLength(Math.Clamp(ActualWidth*.36,280,360));
        split.ColumnDefinitions[1].Width=narrow?new GridLength(0):new GridLength(1,GridUnitType.Star);
        Grid.SetColumn(reader,narrow?0:1);listPane.Visibility=narrow&&narrowDetail&&selected!=null?Visibility.Collapsed:Visibility.Visible;
        reader.Visibility=narrow&&(!narrowDetail||selected==null)?Visibility.Collapsed:Visibility.Visible;
    }
    private static TextBox CopyText(string text,double size=14){var box=new TextBox{Text=text,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,FontSize=size,Foreground=Ui.B("#FCFCFC"),Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(0),MinHeight=0,IsTabStop=true};var template=new ControlTemplate(typeof(TextBox));var frame=new FrameworkElementFactory(typeof(Border));frame.Name="Focus";
        frame.SetValue(Border.BorderThicknessProperty,new Thickness(0,0,0,1));frame.SetValue(Border.BorderBrushProperty,Brushes.Transparent);
        var host=new FrameworkElementFactory(typeof(ScrollViewer));host.Name="PART_ContentHost";frame.AppendChild(host);template.VisualTree=frame;
        var focus=new Trigger{Property=UIElement.IsKeyboardFocusWithinProperty,Value=true};focus.Setters.Add(new Setter(Border.BorderBrushProperty,Ui.B("#F077AF"),"Focus"));template.Triggers.Add(focus);
        box.Template=template;TextBlock.SetLineHeight(box,size*1.75);return box;}
    private void RenderDetail(Article a)
    {
        if(reader==null)return;
        string key=a.Id+a.Summary+a.Abstract+a.Status+a.AbstractStatus+a.Favorite+generating.Contains(a.Id);
        if(detailKey==key)return;
        bool same=detailId==a.Id;detailId=a.Id;double offset=Child<ScrollViewer>(reader)?.VerticalOffset??0;detailKey=key;
        var panel=new DockPanel();var actions=new WrapPanel();
        actions.Children.Add(ActionButton("返回列表",()=>{narrowDetail=false;Adapt();list?.Focus();}));
        actions.Children.Add(ActionButton(a.Favorite?"已收藏":"收藏",()=>{a.Favorite=!a.Favorite;model.Service!.SaveArticle(a);}));
        actions.Children.Add(ActionButton("打开原文",()=>Open(a.Url)));
        var generate=ActionButton(a.Summary.Length>0&&a.SummaryKey.Length>0?"已生成总结":a.Status=="待生成"?"生成总结":"生成／重试",async()=>await Generate(a));
        generate.IsEnabled=!generating.Contains(a.Id)&&!(a.Summary.Length>0&&a.SummaryKey.Length>0);actions.Children.Add(generate);
        DockPanel.SetDock(actions,Dock.Top);panel.Children.Add(actions);
        var content=Stack();content.Children.Add(CopyText(a.ChineseTitle.Length>0?a.ChineseTitle:a.Title,22));
        if(a.ChineseTitle.Length>0){Ui.Gap(content,8);content.Children.Add(CopyText(a.Title,13));}
        Ui.Gap(content);content.Children.Add(Ui.Text(a.DisplayMeta,12));content.Children.Add(Ui.Text(a.DateEvidence,12));Ui.Gap(content,20);
        content.Children.Add(Ui.Text("中文总结 · "+a.Basis,13,"#F0A9C8"));Ui.Gap(content);
        string summary=a.Summary;if(a.ChineseTitle.Length>0&&summary.Split('\n')[0].Trim('#',' ')==a.ChineseTitle)summary=string.Join("\n",summary.Split('\n').Skip(1)).Trim();
        content.Children.Add(CopyText(summary.Length>0?summary:a.HasAbstract?"尚未生成中文总结。可先阅读下方原始摘要，或点击“生成总结”。":"当前可用摘要不足，暂不能总结。可打开原文查看公开摘要。"));
        Ui.Gap(content);content.Children.Add(Ui.Text(a.Status,12));Ui.Gap(content,20);
        var abstractView=new Expander{Header=a.Basis=="基于导读"?"原始导读":"原始摘要",IsExpanded=a.Summary.Length==0,Foreground=Ui.B("#FCFCFC"),Content=CopyText(a.Abstract.Length>0?a.Abstract:"来源尚未提供可用摘要。")};content.Children.Add(abstractView);
        if(a.AbstractStatus.Length>0)content.Children.Add(Ui.Text(a.AbstractStatus,12));
        panel.Children.Add(new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        reader.Content=Ui.Panel(panel,new Thickness(18));if(same)Dispatcher.BeginInvoke(()=>Child<ScrollViewer>(reader)?.ScrollToVerticalOffset(offset));
    }
    private async Task Generate(Article a)
    {
        if(!model.Settings.Ready){status.Text="请先打开右上角阅读设置，配置摘要服务。";return;}
        if(MessageBox.Show(Window.GetWindow(this),"将标题与摘要发送到已配置服务。手动生成不占每日自动额度，可能产生额外服务费用。","生成中文总结",MessageBoxButton.OKCancel)!=MessageBoxResult.OK)return;
        if(!generating.Add(a.Id))return;RenderDetail(a);
        try{await model.Service!.Summarize(a);}catch(Exception e){status.Text=e.Message;}
        finally{generating.Remove(a.Id);RefreshList();}
    }
    private UIElement Subscriptions()
    {
        var pagePanel=new DockPanel();
        var heading=Stack();heading.Children.Add(Ui.Text("选择你的刊物",24,"#FCFCFC"));heading.Children.Add(Ui.Text("首次获取最近七天可取得的条目。来源受限时会明确显示，不代表没有新文章。"));
        var search=new TextBox{Text=sourceSearch,ToolTip="搜索刊物",Margin=new Thickness(0,12,0,8)};heading.Children.Add(Ui.Text("搜索刊物",12));heading.Children.Add(search);
        heading.Children.Add(ActionButton("刷新订阅",async()=>{status.Text="正在检查订阅…";await model.Service!.Refresh();}));
        DockPanel.SetDock(heading,Dock.Top);pagePanel.Children.Add(heading);
        var contents=Stack();sourcesPanel=Stack();contents.Children.Add(sourcesPanel);search.TextChanged+=(_,_)=>{sourceSearch=search.Text;RefreshSources();};
        var custom=new Expander{Header="添加自定义 RSS／Atom",Foreground=Ui.B("#FCFCFC")};var form=Stack();
        var name=new TextBox();var url=new TextBox();form.Children.Add(Ui.Text("刊物名称"));form.Children.Add(name);Ui.Gap(form);form.Children.Add(Ui.Text("订阅地址（HTTP／HTTPS）"));form.Children.Add(url);
        form.Children.Add(ActionButton("添加并订阅",()=>{if(name.Text.Trim().Length==0||!ReadingCatalog.Http(url.Text.Trim())){status.Text="请输入名称及有效订阅地址。";return;}model.Service!.SaveSource(new Source{Id="custom-"+ReadingCatalog.Hash(url.Text.Trim()),Name=name.Text.Trim(),Url=url.Text.Trim(),Publisher="news",Subscribed=true});name.Clear();url.Clear();RefreshSources();}));
        custom.Content=form;contents.Children.Add(custom);pagePanel.Children.Add(new ScrollViewer{Content=contents,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});RefreshSources();return pagePanel;
    }
    private void RefreshSources()
    {
        if(sourcesPanel==null||model.Service==null)return;sourcesPanel.Children.Clear();
        foreach(var source in model.Service.Sources().Where(s=>s.Name.Contains(sourceSearch,StringComparison.OrdinalIgnoreCase)))
        {
            var row=Stack();var check=new CheckBox{Content=source.Name,IsChecked=source.Subscribed,FontSize=14,Foreground=Ui.B("#FCFCFC")};
            check.Click+=(_,_)=>{source.Subscribed=check.IsChecked==true;model.Service.SaveSource(source);};row.Children.Add(check);Ui.Gap(row,6);
            row.Children.Add(Ui.Text(source.Status,12,source.Failures>0?"#F0A9C8":"#C2BEC8"));row.Children.Add(Ui.Text("最近成功："+(source.LastSuccess?.ToLocalTime().ToString("g")??"尚无")+" · 来源条目 "+source.ArticleCount,12));
            var card=Ui.Panel(row,new Thickness(14));card.Margin=new Thickness(0,0,0,10);sourcesPanel.Children.Add(card);
        }
    }
    private static ControlTemplate InputTemplate(Type type){
        var border=new FrameworkElementFactory(typeof(Border));border.Name="Frame";
        border.SetValue(Border.BackgroundProperty,Ui.B("#111111"));border.SetValue(Border.BorderBrushProperty,Ui.B("#514B57"));border.SetValue(Border.BorderThicknessProperty,new Thickness(1));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(8));border.SetValue(Border.PaddingProperty,new Thickness(10,6,10,6));
        var host=new FrameworkElementFactory(typeof(ScrollViewer));host.Name="PART_ContentHost";host.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty,ScrollBarVisibility.Hidden);host.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty,ScrollBarVisibility.Hidden);border.AppendChild(host);
        var template=new ControlTemplate(type){VisualTree=border};var focus=new Trigger{Property=UIElement.IsKeyboardFocusWithinProperty,Value=true};focus.Setters.Add(new Setter(Border.BorderBrushProperty,Ui.B("#F077AF"),"Frame"));template.Triggers.Add(focus);return template;
    }
    private static TextBox Input(string text="")=>new(){Text=text,FontSize=14,Padding=new Thickness(0),MinHeight=40,Template=InputTemplate(typeof(TextBox)),Foreground=Ui.B("#FCFCFC")};
    private UIElement Settings(){
        var outer=new DockPanel();var p=Stack();p.MaxWidth=620;p.HorizontalAlignment=HorizontalAlignment.Left;
        p.Children.Add(Ui.Text("阅读设置",24,"#FCFCFC"));Ui.Gap(p,8);p.Children.Add(Ui.Text("中文总结只发送标题与摘要。没有API也可以订阅和阅读原始摘要。",13));
        var endpoint=Input(model.Settings.Endpoint);var name=Input(model.Settings.Model);
        var key=new PasswordBox{ToolTip="留空保留现有密钥",FontSize=14,Padding=new Thickness(0),MinHeight=40,Foreground=Ui.B("#FCFCFC"),Template=InputTemplate(typeof(PasswordBox))};
        var automatic=new CheckBox{Content="自动生成中文总结",IsChecked=model.Settings.Automatic,Margin=new Thickness(0,12,0,8)};
        var limit=Input(model.Settings.DailyLimit.ToString());limit.MaxWidth=140;limit.HorizontalAlignment=HorizontalAlignment.Left;
        foreach(var pair in new[]{("API地址（HTTPS，通常以 /v1 结尾）",(UIElement)endpoint),("模型名称",name),("API密钥（留空保留，本机加密保存）",key)}){
            Ui.Gap(p,14);p.Children.Add(Ui.Text(pair.Item1,12));Ui.Gap(p,5);p.Children.Add(pair.Item2);}
        p.Children.Add(automatic);p.Children.Add(Ui.Text("每天自动篇数（1–500），手动生成另计服务用量",12));Ui.Gap(p,5);p.Children.Add(limit);
        bool Save(){if(!int.TryParse(limit.Text,out var count)||count<1||count>500){status.Text="每日篇数范围为1–500。";return false;}if(endpoint.Text.Trim().Length==0&&automatic.IsChecked!=true){model.Settings.Automatic=false;model.Settings.DailyLimit=count;model.Service!.Configure(model.Settings);status.Text="自动总结已关闭。";return true;}if(!Uri.TryCreate(endpoint.Text.Trim(),UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.UserInfo.Length>0||uri.Query.Length>0){status.Text="请输入不含密码或查询参数的HTTPS API地址。";return false;}string address=endpoint.Text.Trim().TrimEnd('/');if(model.Settings.AbstractConsent!=address&&MessageBox.Show(Window.GetWindow(this),"生成摘要会发送文章标题和摘要／导读到：\n"+address+"\n是否允许？","摘要发送范围",MessageBoxButton.OKCancel)!=MessageBoxResult.OK)return false;model.Settings.Endpoint=address;model.Settings.Model=name.Text.Trim();if(key.Password.Length>0)model.Settings.SetKey(key.Password);model.Settings.AbstractConsent=address;model.Settings.Automatic=automatic.IsChecked==true;model.Settings.DailyLimit=count;model.Service!.Configure(model.Settings);status.Text="阅读设置已保存。";return true;}
        var actions=new WrapPanel{Margin=new Thickness(0,12,0,0)};
        actions.Children.Add(ActionButton("保存设置",()=>Save()));
        actions.Children.Add(ActionButton("测试连接",async()=>{if(!Save())return;try{status.Text=await model.Service!.Test();}catch(Exception ex){status.Text=ex.Message;}}));
        actions.Children.Add(ActionButton("返回文章",()=>Show(0)));DockPanel.SetDock(actions,Dock.Bottom);outer.Children.Add(actions);
        Ui.Gap(p,18);p.Children.Add(Ui.Text("订阅每两小时检查。普通文章保留90天，收藏长期保留。停用后停止更新，保留已有数据。",12));
        p.Children.Add(ActionButton("停用阅读后台",()=>{if(model.Service!=null)model.Service.Updated-=Update;model.Disable();Show(0);}));
        outer.Children.Add(new ScrollViewer{Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});return outer;
    }
    private static void Open(string url){if(ReadingCatalog.Http(url))System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url){UseShellExecute=true});}
}
