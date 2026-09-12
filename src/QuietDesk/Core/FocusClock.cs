using System;
using System.Collections.Generic;

namespace QuietDesk;

// The caller supplies monotonic elapsed time; suspend/resume resets the baseline.
public sealed class FocusClock
{
    public double Remaining {get;private set;}=25*60;
    public int Minutes {get;private set;}=25;
    public bool Running {get;private set;}
    public bool Completed {get;private set;}
    public Dictionary<string,double> Daily {get;}
    public FocusClock(Dictionary<string,double> daily)=>Daily=daily;
    public void Configure(int minutes) {Minutes=Math.Clamp(minutes,1,240);Reset();}
    public void Toggle() {if(Completed)Reset();Running=!Running;}
    public void Pause()=>Running=false;
    public void Reset() {Running=false;Completed=false;Remaining=Minutes*60;}
    public void Advance(double seconds,DateTimeOffset end)
    {
        if(!Running || !double.IsFinite(seconds)||seconds<=0)return;
        // Large unexplained gaps are not counted as productive time.
        if(seconds>10)return;
        var used=Math.Min(seconds,Remaining);Remaining-=used;
        var start=end-TimeSpan.FromSeconds(used);
        while(start<end) {
            var midnight=new DateTimeOffset(start.Date.AddDays(1),start.Offset);
            var stop=midnight<end?midnight:end;var key=start.ToString("yyyy-MM-dd");
            Daily[key]=Daily.GetValueOrDefault(key)+(stop-start).TotalSeconds;start=stop;
        }
        if(Remaining<=0) {Remaining=0;Running=false;Completed=true;}
    }
}
