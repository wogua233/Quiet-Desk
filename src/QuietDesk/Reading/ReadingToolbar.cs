using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace QuietDesk.Reading;

internal sealed partial class ReadingView
{
    private readonly Button sourcePicker,filterPicker,refreshButton,queueButton;
    private Popup? filterPopup;
    private ContextMenu? activeMenu;

    private static Button CompactButton(string title,Action action)
    {
        var button=Ui.Button(title,action,feedback:false);
        button.MinHeight=32;button.Height=32;button.Padding=new Thickness(10,3,10,3);
        button.FontSize=13;button.Margin=new Thickness(0,0,6,4);button.VerticalAlignment=VerticalAlignment.Top;
        return button;
    }

    private void CloseFlyouts()
    {
        if(filterPopup!=null){filterPopup.IsOpen=false;filterPopup=null;}
        if(activeMenu!=null){activeMenu.IsOpen=false;activeMenu=null;}
    }

    private ContextMenu Menu()
    {
        var menu=new ContextMenu{Background=Ui.B("#222224"),Foreground=Ui.B("#FCFCFC"),BorderBrush=Ui.B("#514B57"),BorderThickness=new Thickness(1),Padding=new Thickness(4),MaxHeight=Math.Max(180,Math.Min(480,SystemParameters.WorkArea.Height-80))};
        var frame=new FrameworkElementFactory(typeof(Border));frame.SetValue(Border.BackgroundProperty,menu.Background);frame.SetValue(Border.BorderBrushProperty,menu.BorderBrush);frame.SetValue(Border.BorderThicknessProperty,new Thickness(1));frame.SetValue(Border.CornerRadiusProperty,new CornerRadius(8));frame.SetValue(Border.PaddingProperty,new Thickness(4));
        var scroll=new FrameworkElementFactory(typeof(ScrollViewer));scroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty,ScrollBarVisibility.Auto);scroll.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty,ScrollBarVisibility.Disabled);
        scroll.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter)));frame.AppendChild(scroll);menu.Template=new ControlTemplate(typeof(ContextMenu)){VisualTree=frame};
        var style=new Style(typeof(MenuItem));style.Setters.Add(new Setter(Control.ForegroundProperty,Ui.B("#FCFCFC")));
        style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(10,7,10,7)));
        var highlight=new Trigger{Property=MenuItem.IsHighlightedProperty,Value=true};
        highlight.Setters.Add(new Setter(Control.BackgroundProperty,Ui.B("#513244")));style.Triggers.Add(highlight);
        menu.Resources.Add(typeof(MenuItem),style);return menu;
    }

    private void OpenMenu(Button anchor,ContextMenu menu)
    {
        CloseFlyouts();activeMenu=menu;menu.PlacementTarget=anchor;menu.Placement=PlacementMode.Bottom;
        menu.Closed+=(_,_)=>{if(activeMenu==menu)activeMenu=null;};menu.IsOpen=true;
    }

    private void OpenSources()
    {
        if(model.Service==null)return;
        var menu=Menu();
        foreach(var source in new[]{new Source{Id=ReadingCatalog.SubscribedFilter,Name="订阅刊物"},new Source{Id="",Name="全部刊物"}}.Concat(model.Service.Sources().OrderBy(s=>s.Name)))
        {
            var item=new MenuItem{Header=source.Name,IsCheckable=true,IsChecked=model.SourceId==source.Id,ToolTip=source.Name};
            item.Click+=(_,_)=>{model.SourceId=source.Id;ResetFilter();};menu.Items.Add(item);
        }
        OpenMenu(sourcePicker,menu);
    }

    private void OpenQueue()
    {
        if(model.Service==null)return;
        var menu=Menu();
        var start=new MenuItem{Header="立即开始"};start.Click+=async(_,_)=>await StartBatch(false);
        var pause=new MenuItem{Header="暂停队列"};pause.Click+=(_,_)=>model.Service?.PauseQueue();
        menu.Items.Add(start);menu.Items.Add(pause);menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem{Header=new TextBlock{Text=model.Service.ScheduleStatus+"\n"+model.Service.QueueStatus,TextWrapping=TextWrapping.Wrap,MaxWidth=340,FontSize=12},IsEnabled=false});
        OpenMenu(queueButton,menu);
    }

    private void OpenFilters()
    {
        if(model.Service==null)return;
        CloseFlyouts();
        // Keep edits local until Apply; dismissing the flyout leaves the reader untouched.
        var form=new StackPanel();
        var heading=Ui.Text("日期、排序与筛选",16,"#FCFCFC");heading.Margin=new Thickness(0,0,0,12);form.Children.Add(heading);
        ComboBox Choice(string[] options,int selected)=>new(){ItemsSource=options,SelectedIndex=selected,MinHeight=38};
        void Field(string label,UIElement control){var text=Ui.Text(label,12);text.Margin=new Thickness(0,8,0,5);form.Children.Add(text);form.Children.Add(control);System.Windows.Automation.AutomationProperties.SetName(control,label);}
        var range=Choice(new[]{"最近7天","指定日期"},model.Recent?0:1);Field("时间范围",range);
        var date=Input(model.SelectedDay.ToString("yyyy-MM-dd"));date.ToolTip="日期（yyyy-MM-dd）；最近7天以此日为结束日";Field("日期／范围结束日",date);
        var dateActions=new WrapPanel{Margin=new Thickness(0,8,0,0)};
        var validation=Ui.Text("",12,"#F0A9C8");
        bool ReadDay(out DateTime value){bool ok=DateTime.TryParseExact(date.Text.Trim(),"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out value)&&value>=DateTime.MinValue.AddDays(6);validation.Text=ok?"":"请按 yyyy-MM-dd 输入有效日期。";return ok;}
        void Shift(int days){if(ReadDay(out var value)){try{date.Text=value.AddDays(days).ToString("yyyy-MM-dd");range.SelectedIndex=1;}catch(ArgumentOutOfRangeException){validation.Text="日期超出可用范围。";}}}
        dateActions.Children.Add(CompactButton("前一天",()=>Shift(-1)));
        dateActions.Children.Add(CompactButton("今天",()=>{date.Text=DateTime.Today.ToString("yyyy-MM-dd");range.SelectedIndex=1;}));
        dateActions.Children.Add(CompactButton("后一天",()=>Shift(1)));form.Children.Add(dateActions);
        var mode=Choice(new[]{"按发表日期","按发现日期"},model.Discovered?1:0);Field("日期依据",mode);
        var order=Choice(new[]{"时间：更新的在前","时间：更老的在前","期刊名称：A–Z"},model.SortByJournal?2:model.NewestFirst?0:1);Field("排序",order);
        var favorites=new CheckBox{Content="收藏（全部日期）",IsChecked=model.Favorites,Margin=new Thickness(0,10,0,0)};
        var unread=new CheckBox{Content="仅未读",IsChecked=model.Unread};form.Children.Add(favorites);form.Children.Add(unread);form.Children.Add(validation);
        var popup=new Popup{PlacementTarget=filterPicker,Placement=PlacementMode.Bottom,StaysOpen=false,AllowsTransparency=true,PopupAnimation=PopupAnimation.None};
        var actions=new WrapPanel{Margin=new Thickness(0,12,0,0)};
        void Apply(){if(!ReadDay(out var value)){date.Focus();return;}model.SelectedDay=value;model.Recent=range.SelectedIndex==0;model.Discovered=mode.SelectedIndex==1;model.SortByJournal=order.SelectedIndex==2;if(!model.SortByJournal)model.NewestFirst=order.SelectedIndex==0;model.Favorites=favorites.IsChecked==true;model.Unread=unread.IsChecked==true;ResetFilter();filterPicker.Focus();}
        actions.Children.Add(CompactButton("应用筛选",Apply));actions.Children.Add(CompactButton("取消",()=>{popup.IsOpen=false;filterPicker.Focus();}));form.Children.Add(actions);
        var scroll=new ScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,MaxHeight=Math.Max(160,Math.Min(520,SystemParameters.WorkArea.Height-100))};
        var frame=new Border{Child=scroll,Background=Ui.B("#222224"),BorderBrush=Ui.B("#514B57"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Padding=new Thickness(14),Width=Math.Clamp(ActualWidth-24,280,400)};
        KeyboardNavigation.SetTabNavigation(frame,KeyboardNavigationMode.Cycle);
        frame.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){popup.IsOpen=false;filterPicker.Focus();e.Handled=true;}};
        date.KeyDown+=(_,e)=>{if(e.Key==Key.Enter){Apply();e.Handled=true;}};
        popup.Child=frame;filterPopup=popup;popup.Closed+=(_,_)=>{if(filterPopup==popup)filterPopup=null;};
        popup.Opened+=(_,_)=>range.Focus();popup.IsOpen=true;
    }
}
