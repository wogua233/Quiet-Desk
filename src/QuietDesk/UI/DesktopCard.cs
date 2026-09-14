using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
namespace QuietDesk;
internal static class DesktopCard
{
 internal static FrameworkElement Create(PlayerModel model,DesktopHost host,Action open)
 {
  var stack=new StackPanel();var title=Ui.Text("静隅  ·  桌面",12,"#C2BEC8");title.Height=24;title.ToolTip="拖动此处移动组件";stack.Children.Add(title);
  var name=Ui.Bound(model,"SceneName",17,"#FCFCFC");name.TextWrapping=TextWrapping.NoWrap;name.TextTrimming=TextTrimming.CharacterEllipsis;name.SetBinding(FrameworkElement.ToolTipProperty,new Binding("SceneName"){Source=model});stack.Children.Add(name);Ui.Gap(stack,10);
  var actions=new Grid();actions.ColumnDefinitions.Add(new());actions.ColumnDefinitions.Add(new(){Width=new GridLength(52)});var play=Ui.Button("",()=>_ = model.TogglePlay(),true);Ui.BindContent(play,model,"PlayLabel");play.Padding=new Thickness(4);actions.Children.Add(play);var launch=Ui.Button("打开",open);launch.Padding=new Thickness(4);launch.Margin=new Thickness(5,0,0,0);Grid.SetColumn(launch,1);actions.Children.Add(launch);stack.Children.Add(actions);stack.Children.Add(Ui.Slider(model,"Master","桌面总音量"));
  var digits=new NixieClock{Margin=new Thickness(0,3,0,5),ToolTip="静默专注计时 · 辉光管"};digits.SetBinding(NixieClock.TextProperty,new Binding("FocusText"){Source=model});stack.Children.Add(digits);var focus=Ui.Button("",model.ToggleFocus);focus.Padding=new Thickness(5);Ui.BindContent(focus,model,"FocusLabel");stack.Children.Add(focus);Ui.Gap(stack,7);
  var more=new StackPanel();bool expanded=host.HeightDip>320;var mix=Ui.Button(expanded?"收起混音":"调整混音",()=>{});mix.MinHeight=30;mix.Padding=new Thickness(5);stack.Children.Add(mix);
  var locked=new CheckBox{Content=host.Locked?"位置已锁定":"锁定位置",IsChecked=host.Locked,FontSize=12};locked.Checked+=(_,_)=>{host.Locked=true;locked.Content="位置已锁定";title.Cursor=System.Windows.Input.Cursors.Arrow;};locked.Unchecked+=(_,_)=>{host.Locked=false;locked.Content="锁定位置";title.Cursor=System.Windows.Input.Cursors.SizeAll;};stack.Children.Add(locked);stack.Children.Add(more);
  void Refresh(){more.Children.Clear();more.Visibility=expanded?Visibility.Visible:Visibility.Collapsed;if(expanded){foreach(var c in model.Channels.Where(c=>c.Enabled)){more.Children.Add(Ui.Text(c.Info.Name,12));more.Children.Add(Ui.Slider(c,"Volume",c.Info.Name+"音量"));}more.Children.Add(Ui.Button("选择更多声音",open));host.Resize(320+model.Channels.Count(c=>c.Enabled)*50+42);}}
  mix.Click+=(_,_)=>{expanded=!expanded;mix.Content=expanded?"收起混音":"调整混音";if(!expanded)host.Resize(320);Refresh();};
  System.ComponentModel.PropertyChangedEventHandler changed=(_,e)=>{if(e.PropertyName=="Enabled")Refresh();};
  var root=new Border{Width=220,Height=host.HeightDip,Child=stack,Padding=new Thickness(16,12,16,10),Background=Ui.Gradient(),CornerRadius=new CornerRadius(12),BorderThickness=new Thickness(1),BorderBrush=Ui.B("#514B57"),SnapsToDevicePixels=true,UseLayoutRounding=true};if(host.TransparencyAvailable)System.Windows.Data.BindingOperations.SetBinding(root.Background,Brush.OpacityProperty,new Binding("DesktopOpacity"){Source=model});root.Loaded+=(_,_)=>{foreach(var c in model.Channels)c.PropertyChanged+=changed;Refresh();};root.Unloaded+=(_,_)=>{foreach(var c in model.Channels)c.PropertyChanged-=changed;};return root;
 }
}
