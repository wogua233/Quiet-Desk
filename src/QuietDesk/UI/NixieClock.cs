using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Media;

namespace QuietDesk;

// Static vector cathodes: only a changed time or size invalidates this drawing.
// No timer, blur shader, animation, or off-screen surface is allocated here.
internal sealed class NixieClock:FrameworkElement
{
    public static readonly DependencyProperty TextProperty=DependencyProperty.Register(nameof(Text),typeof(string),typeof(NixieClock),new FrameworkPropertyMetadata("25:00",FrameworkPropertyMetadataOptions.AffectsRender));
    public string Text{get=>(string)GetValue(TextProperty);set=>SetValue(TextProperty,value);}
    private static readonly Geometry[] Digits=new[]{
        "M12,3 C2,3 3,15 3,20 C3,28 3,37 12,37 C21,37 21,28 21,20 C21,11 22,3 12,3",
        "M5,10 L12,3 L12,37 M5,37 L19,37",
        "M3,11 C3,0 21,0 21,11 C21,20 6,24 3,37 L22,37",
        "M3,6 C17,-2 27,12 13,19 C27,20 23,43 3,34",
        "M18,37 L18,3 L2,26 L23,26",
        "M21,3 L4,3 L4,18 C24,9 28,37 12,37 C7,37 4,35 2,32",
        "M20,5 C1,-5 -3,38 12,37 C25,37 25,15 12,17 C5,17 3,22 3,25",
        "M2,3 L22,3 C13,16 10,27 8,37",
        "M12,19 C-4,14 3,2 12,3 C23,2 28,15 12,19 C-5,24 1,38 12,37 C24,38 29,24 12,19",
        "M4,35 C23,45 28,2 12,3 C-2,2 -2,25 12,23 C19,23 21,18 21,15"
    }.Select(s=>{var g=Geometry.Parse(s);g.Freeze();return g;}).ToArray();
    private static Pen Wire(string color,double width){var p=new Pen(Ui.B(color),width){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};p.Freeze();return p;}
    private static readonly Pen Ghost=Wire("#30221D",.6),Halo=Wire("#25F67A35",6),Glow=Wire("#65FB8143",3),Cathode=Wire("#FFBA7A",1.25),Core=Wire("#FFF0D4",.35),Glass=Wire("#59483D",.7);
    private static readonly Brush Tube=MakeTube();
    private static Brush MakeTube(){var b=new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#282321"),(Color)ColorConverter.ConvertFromString("#151314"),0);b.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#171518"),.45));b.Freeze();return b;}
    public NixieClock(){Height=52;MinWidth=110;IsHitTestVisible=false;SnapsToDevicePixels=true;}
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);string value=string.IsNullOrEmpty(Text)?"00:00":Text;
        if(value.Length>8||value.Any(c=>!char.IsAsciiDigit(c)&&c!=':'))return;
        int digits=value.Count(char.IsAsciiDigit),dots=value.Length-digits;
        double natural=digits*34+dots*11,scale=Math.Min(1,ActualWidth/natural),x=(ActualWidth-natural*scale)/2;
        dc.PushClip(new RectangleGeometry(new Rect(0,0,ActualWidth,ActualHeight)));
        dc.PushTransform(new TranslateTransform(x,2));dc.PushTransform(new ScaleTransform(scale,Math.Min(scale,(ActualHeight-4)/48)));
        double left=0;
        foreach(char c in value){
            if(c==':'){dc.DrawEllipse(Ui.B("#FFC08C"),null,new Point(left+4,19),1.2,1.2);dc.DrawEllipse(Ui.B("#FFC08C"),null,new Point(left+4,29),1.2,1.2);left+=11;continue;}
            dc.DrawRoundedRectangle(Tube,Glass,new Rect(left,0,30,47),9,9);
            dc.DrawLine(Wire("#43665346",1),new Point(left+4,11),new Point(left+4,34));
            dc.DrawLine(Wire("#5A473C",1.3),new Point(left+8,45),new Point(left+22,45));
            dc.PushTransform(new TranslateTransform(left+3,3));
            dc.DrawGeometry(null,Ghost,Digits[8]);var digit=Digits[c-'0'];
            dc.DrawGeometry(null,Halo,digit);dc.DrawGeometry(null,Glow,digit);dc.DrawGeometry(null,Cathode,digit);dc.DrawGeometry(null,Core,digit);
            dc.Pop();left+=34;
        }
        dc.Pop();dc.Pop();dc.Pop();
    }
    protected override AutomationPeer OnCreateAutomationPeer()=>new ClockPeer(this);
    private sealed class ClockPeer(NixieClock clock):FrameworkElementAutomationPeer(clock)
    {
        protected override string GetNameCore()=>"专注剩余时间 "+clock.Text;
        protected override AutomationControlType GetAutomationControlTypeCore()=>AutomationControlType.Text;
    }
}
