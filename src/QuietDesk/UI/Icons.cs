using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace QuietDesk;

internal static class Icons
{
    internal static FrameworkElement Create(string key,double size=22,string color="#F077AF")
    {
        string data=key switch {
            "rain" or "windowrain"=>"M3,9 Q3,4 8,5 Q11,0 15,5 Q22,4 21,10 L4,10 M7,14 L5,19 M13,14 L11,19 M19,14 L17,19",
            "waves" or "stream"=>"M2,6 Q5,2 9,6 T16,6 T23,6 M2,12 Q5,8 9,12 T16,12 T23,12 M2,18 Q5,14 9,18 T16,18 T23,18",
            "wind"=>"M2,7 L17,7 Q23,7 21,3 Q19,0 17,3 M2,12 L21,12 M2,17 L14,17 Q20,17 18,21 Q16,24 14,21",
            "birds"=>"M2,12 Q7,4 12,12 Q17,4 22,12 M5,18 Q8,13 11,18",
            "fireplace"=>"M12,2 Q18,9 15,11 Q22,9 20,16 Q17,24 8,21 Q1,18 5,10 Q5,16 10,11 Q13,7 12,2 Z",
            "storm"=>"M3,10 Q2,4 8,5 Q12,0 16,5 Q23,4 22,11 L15,11 M12,9 L7,16 L12,16 L9,23",
            "night"=>"M16,2 A10,10 0 1 0 22,17 A10,10 0 0 1 16,2 M3,3 L3,7 M1,5 L5,5",
            "leaves"=>"M4,20 Q0,3 21,3 Q21,22 4,20 Z M4,20 L16,8 M10,14 L10,8 M10,14 L16,14",
            "cafe"=>"M3,8 L17,8 L17,15 Q17,21 10,21 Q3,21 3,15 Z M17,9 Q24,8 22,13 Q21,16 17,15 M7,2 L7,5 M12,2 L12,5",
            "library"=>"M12,5 Q7,1 2,4 L2,20 Q7,17 12,21 Q17,17 22,20 L22,4 Q17,1 12,5 L12,21",
            "train"=>"M5,3 Q12,0 19,3 L19,18 L5,18 Z M5,7 L19,7 M12,7 L12,13 M5,13 L19,13 M7,21 L5,24 M17,21 L19,24",
            "fan"=>"M12,10 Q1,0 5,12 L10,13 Q2,23 14,20 L14,15 Q25,20 21,9 L15,10 Q20,0 10,3 Z",
            "keyboard"=>"M2,5 L22,5 L22,20 L2,20 Z M5,9 L7,9 M10,9 L12,9 M16,9 L19,9 M5,13 L7,13 M10,13 L12,13 M16,13 L19,13 M6,17 L18,17",
            "play"=>"M7,3 L21,12 L7,21 Z", "pause"=>"M7,3 L7,21 M17,3 L17,21",
            "heart"=>"M12,21 Q-3,11 3,5 Q8,0 12,6 Q17,0 22,5 Q27,11 12,21 Z",
            "settings"=>"M3,6 L21,6 M3,12 L21,12 M3,18 L21,18 M8,3 L8,9 M16,9 L16,15 M9,15 L9,21",
            _=>"M3,10 L3,14 M7,5 L7,19 M12,2 L12,22 M17,6 L17,18 M22,10 L22,14"
        };
        var p=new Path{Data=Geometry.Parse(data),Stroke=Ui.B(color),StrokeThickness=1.5,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,Stretch=Stretch.Uniform,Width=size,Height=size,IsHitTestVisible=false};return p;
    }
}
