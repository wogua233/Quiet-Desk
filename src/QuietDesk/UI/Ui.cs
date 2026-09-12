using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace QuietDesk;

internal static class Ui
{
    internal static Action? ClickFeedback;
    internal static Func<bool>? MotionReduced;
    internal static LinearGradientBrush Gradient(string top="#27272B",string bottom="#19191C",double opacity=1){var b=new LinearGradientBrush((Color)ColorConverter.ConvertFromString(top),(Color)ColorConverter.ConvertFromString(bottom),90){Opacity=opacity};return b;}
    internal static SolidColorBrush B(string hex)=>DesktopHost.Brush(hex);
    internal static TextBlock Text(string text,double size=13,string color="#C2BEC8")=>new(){Text=text,FontSize=Math.Max(12,size),Foreground=B(color),TextWrapping=TextWrapping.Wrap};
    internal static TextBlock Bound(object model,string path,double size=13,string color="#C2BEC8") {var t=Text("",size,color);if(path=="FocusText")t.FontFamily=new FontFamily("Consolas");t.SetBinding(TextBlock.TextProperty,new Binding(path){Source=model});return t;}
    internal static Button Button(string text,Action click,bool primary=false,bool feedback=true) {var b=new Button{Content=text};System.Windows.Automation.AutomationProperties.SetName(b,text);if(primary){b.Background=B("#F077AF");b.Foreground=B("#222224");b.BorderBrush=B("#F077AF");}b.Click+=(_,_)=>{if(feedback)ClickFeedback?.Invoke();click();};b.RenderTransform=new TranslateTransform();b.PreviewMouseLeftButtonDown+=(_,_)=>{if(b.IsVisible&&SystemParameters.ClientAreaAnimation&&MotionReduced?.Invoke()!=true)((TranslateTransform)b.RenderTransform).BeginAnimation(TranslateTransform.YProperty,new System.Windows.Media.Animation.DoubleAnimation(1,TimeSpan.FromMilliseconds(80)));};b.PreviewMouseLeftButtonUp+=(_,_)=>{var transform=(TranslateTransform)b.RenderTransform;if(SystemParameters.ClientAreaAnimation&&MotionReduced?.Invoke()!=true)transform.BeginAnimation(TranslateTransform.YProperty,new System.Windows.Media.Animation.DoubleAnimation(0,TimeSpan.FromMilliseconds(120)));else{transform.BeginAnimation(TranslateTransform.YProperty,null);transform.Y=0;}};b.MouseLeave+=(_,_)=>((TranslateTransform)b.RenderTransform).BeginAnimation(TranslateTransform.YProperty,null);return b;}
    internal static FrameworkElement Slider(object model,string path,string label) {
        var row=new DockPanel{LastChildFill=true};var value=Text("",12,"#C2BEC8");value.Width=40;value.TextAlignment=TextAlignment.Right;value.VerticalAlignment=VerticalAlignment.Center;value.SetBinding(TextBlock.TextProperty,new Binding(path){Source=model,StringFormat="{0:P0}"});DockPanel.SetDock(value,Dock.Right);row.Children.Add(value);
        var s=new Slider{SmallChange=.01,LargeChange=.1};s.SetBinding(System.Windows.Controls.Primitives.RangeBase.ValueProperty,new Binding(path){Source=model,Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged});System.Windows.Automation.AutomationProperties.SetName(s,label);
        s.PreviewMouseWheel+=(_,e)=>{if(s.IsKeyboardFocusWithin){s.Value=Math.Clamp(s.Value+(e.Delta>0?.01:-.01),0,1);e.Handled=true;}};
        row.Children.Add(s);return row;
    }
    internal static Border Panel(UIElement child,Thickness? padding=null,string color="#222224")=>new(){Child=child,Background=Gradient(),BorderBrush=B("#333036"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=padding??new Thickness(18)};
    internal static void Gap(Panel panel,double height=12)=>panel.Children.Add(new Border{Height=height});
    internal static void BindContent(Button button,object model,string path) {
        button.SetBinding(System.Windows.Automation.AutomationProperties.NameProperty,new Binding(path){Source=model});
        if(path!="PlayLabel"){button.SetBinding(ContentControl.ContentProperty,new Binding(path){Source=model});return;}
        var row=new StackPanel{Orientation=Orientation.Horizontal};var icon=new ContentControl{Margin=new Thickness(0,0,7,0),VerticalAlignment=VerticalAlignment.Center};row.Children.Add(icon);var text=Bound(model,path,13);text.SetBinding(TextBlock.ForegroundProperty,new Binding("Foreground"){Source=button});row.Children.Add(text);button.Content=row;
        void Paint(){if(model is PlayerModel player)icon.Content=Icons.Create(player.Playing?"pause":"play",13,"#333036");}System.ComponentModel.PropertyChangedEventHandler changed=(_,e)=>{if(e.PropertyName=="Playing")Paint();};button.Loaded+=(_,_)=>{if(model is System.ComponentModel.INotifyPropertyChanged n)n.PropertyChanged+=changed;Paint();};button.Unloaded+=(_,_)=>{if(model is System.ComponentModel.INotifyPropertyChanged n)n.PropertyChanged-=changed;};Paint();
    }
}
