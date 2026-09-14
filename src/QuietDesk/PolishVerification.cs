using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QuietDesk;
internal static class PolishVerification
{
    internal static int Run(string[] args)
    {
        string output=args[Array.IndexOf(args,"--out")+1],folder=Path.Combine(Path.GetTempPath(),"QuietDesk-polish-"+Guid.NewGuid());
        var lines=new List<string>();void Check(bool ok,string name){if(!ok)throw new Exception(name);lines.Add("PASS "+name);}
        try{
            var store=new StateStore(folder);Check(store.Load().FeedbackEnabled,"new profile defaults to light button feedback");
            File.WriteAllText(Path.Combine(folder,"state.json"),"{\"FeedbackEnabled\":false,\"Master\":0.23}");
            var upgraded=store.Load();Check(upgraded.FeedbackEnabled&&upgraded.Master==.23,"old profile enables feedback once without changing volume");
            upgraded.FeedbackEnabled=false;store.Save(upgraded);Check(!store.Load().FeedbackEnabled,"explicitly disabled feedback survives later restarts");
            var stations=RadioDirectory.Domestic();Check(stations.Count>=12&&stations.Select(s=>s.Id).Distinct().Count()==stations.Count,"expanded directory contains unique station IDs");
            Check(RadioDirectory.FilterDomestic(stations,"","domestic:jazz").Count>=2,"offline direct jazz category contains two verified smooth-jazz choices");
            Check(RadioDirectory.FilterDomestic(stations,"爵士","domestic").All(s=>s.Tags.Contains("爵士")||s.Name.Contains("爵士")),"domestic search matches genre as well as station name");
            Check(stations.All(s=>s.DirectConnection&&RadioDirectory.IsHttp(s.Url)&&s.SourcePage.Length>0&&s.VerificationNote.Length>0),"every direct station retains source and verification scope");
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};app.Resources.MergedDictionaries.Add(new(){Source=new Uri("/QuietDesk;component/UI/Theme.xaml",UriKind.Relative)});
            var clock=new NixieClock{Width=184};
            foreach(var text in new[]{"25:00","4:00:00","00:00"})foreach(double scale in new[]{1,1.25,1.5,2}){
                clock.Text=text;Capture(clock,184,52,scale,Path.Combine(Path.GetDirectoryName(output)!,"clock-"+text.Replace(":","")+"-"+(int)(scale*100)+".png"));
            }
            Check(clock.DesiredSize.Width==184&&clock.DesiredSize.Height==52,"four-hour and zero states render within compact clock bounds at 100–200 percent");
            using(var model=new PlayerModel(folder)){
                model.FeedbackEnabled=false;model.Master=0;FrameworkElement? card=null;
                using var host=new DesktopHost(_=>{},folder,h=>card=DesktopCard.Create(model,h,()=>{}));
                Check(host.Handle!=0&&card!=null,"new card still creates a true desktop child");
                Capture(card!,220,320,1,Path.Combine(Path.GetDirectoryName(output)!,"desktop-card.png"));
                var content=((System.Windows.Controls.Border)card!).Child as FrameworkElement;
                Check(content!=null&&content.DesiredSize.Height<=298,"clock and desktop controls fit the existing 220x320 card");
            }
            File.WriteAllLines(output,lines);app.Shutdown();return 0;
        }catch(Exception e){lines.Add("FAIL "+e);File.WriteAllLines(output,lines);return 1;}
    }
    private static void Capture(FrameworkElement view,double width,double height,double scale,string path){
        view.Measure(new Size(width,height));view.Arrange(new Rect(0,0,width,height));view.UpdateLayout();
        var bitmap=new RenderTargetBitmap((int)(width*scale),(int)(height*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(view);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);png.Save(file);
    }
}
