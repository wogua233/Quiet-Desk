using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace QuietDesk;
internal static class SceneSelectionVerification
{
    private static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {
        yield return root;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach(var nested in Walk(child))yield return nested;
    }
    private static Button? Choice(Window window,string name)=>Walk(window).OfType<Button>().FirstOrDefault(b=>AutomationProperties.GetName(b)==name);
    private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    internal static int Run(string[] args)
    {
        var lines=new List<string>();var index=Array.IndexOf(args,"--out");var output=index>=0?args[index+1]:"scene-test.txt";
        var folder=Path.Combine(Path.GetTempPath(),"QuietDesk-scene-selection-"+Guid.NewGuid());
        try{
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/QuietDesk;component/UI/Theme.xaml",UriKind.Relative)});
            using(var model=new PlayerModel(folder)){
                model.Master=0;var window=new MainWindow(model);
                var query=Walk(window).OfType<TextBox>().Single(t=>AutomationProperties.GetName(t)=="搜索离线声音");query.Text="雨";
                model.ApplyScene(new Scene{Name="准备",Levels=new(){{"rain",.31},{"wind",.22}}});model.SaveScene("保存回归组合");
                Check(Choice(window,"保存回归组合")!=null,"Saved scene absent from sound page");
                Check(ReferenceEquals(query,Walk(window).OfType<TextBox>().Single(t=>AutomationProperties.GetName(t)=="搜索离线声音"))&&query.Text=="雨","Saving rebuilt page or cleared search");lines.Add("PASS saved scene appears immediately without rebuilding sound page");
                model.ApplyScene(new Scene{Name="其他",Levels=new(){{"birds",.7}}});Choice(window,"保存回归组合")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(model.Channels.Single(c=>c.Info.Id=="rain").Enabled&&Math.Abs(model.Channels.Single(c=>c.Info.Id=="rain").Volume-.31)<.001&&!model.Channels.Single(c=>c.Info.Id=="birds").Enabled&&!model.Playing,"Saved selection did not restore mix quietly");lines.Add("PASS selecting saved choice restores channels and volumes without autoplay");
                model.SaveScene("保存回归组合");Check(model.Scenes.Count==1&&Walk(window).OfType<Button>().Count(b=>AutomationProperties.GetName(b)=="保存回归组合")==1,"Overwrite duplicated choice");lines.Add("PASS overwrite retains a single choice");
                model.RenameScene(model.Scenes[0],"重命名回归组合");Check(Choice(window,"保存回归组合")==null&&Choice(window,"重命名回归组合")!=null,"Rename stale choice");
                var beforeDelete=model.Channels.Where(c=>c.Enabled).ToDictionary(c=>c.Info.Id,c=>c.Volume);Choice(window,"删除组合：重命名回归组合")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(model.Scenes.Count==0&&Choice(window,"重命名回归组合")==null,"Deleted choice remains");Check(beforeDelete.All(p=>model.Channels.Any(c=>c.Info.Id==p.Key&&c.Enabled&&c.Volume==p.Value))&&!model.Playing,"Deletion changed playback");Check(Choice(window,"撤销删除")?.IsEnabled==true,"Undo not accessible after last deletion");Choice(window,"撤销删除")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(Choice(window,"撤销删除")?.IsEnabled==false,"Undo should be disabled after restoring");Check(Choice(window,"重命名回归组合")!=null,"Undo absent");lines.Add("PASS rename, delete and undo update choices immediately");
                window.Navigate(2);window.Navigate(0);Check(Choice(window,"重命名回归组合")!=null&&query.Text=="雨","Cached page loses choices or search");lines.Add("PASS page navigation preserves saved choices and search");
            }
            using(var restored=new PlayerModel(folder)){
                var window=new MainWindow(restored);Check(restored.Scenes.Count==1&&Choice(window,"重命名回归组合")!=null&&!restored.Playing,"Restart loses saved choice");lines.Add("PASS saved choice survives restart without autoplay");Choice(window,"删除组合：重命名回归组合")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            using(var deleted=new PlayerModel(folder)){Check(deleted.Scenes.Count==0,"Deleted scene reappeared after restart");lines.Add("PASS deleting via visible button persists across restart");}
            File.WriteAllLines(output,lines);return 0;
        }catch(Exception e){lines.Add("FAIL "+e);File.WriteAllLines(output,lines);return 1;}
    }
}
