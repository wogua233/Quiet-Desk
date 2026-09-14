using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
namespace QuietDesk.Reading;
internal sealed class ReadingIdleGate
{
    [StructLayout(LayoutKind.Sequential)] private struct Input {public uint Size,Tick;}
    [StructLayout(LayoutKind.Sequential)] private struct Power {public byte AC,Battery,Percent,Saver;public uint Life,Full;}
    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref Input input);
    [DllImport("kernel32.dll")] private static extern bool GetSystemTimes(out long idle,out long kernel,out long user);
    [DllImport("kernel32.dll")] private static extern bool GetSystemPowerStatus(out Power power);
    [DllImport("shell32.dll")] private static extern int SHQueryUserNotificationState(out int state);
    private long lastIdle,lastKernel,lastUser,lastSample;
    private long lowSince=Stopwatch.GetTimestamp();
    internal string Reason {get;private set;}="等待空闲";
    internal static bool CanRun(uint inactive,double lowSeconds,bool saver,bool presentation)=>inactive>=180000&&lowSeconds>=30&&!saver&&!presentation;
    internal static bool TimeReady(ReadingSettings settings,DateTime now)=>!settings.Scheduled||(TimeOnly.TryParseExact(settings.ScheduledTime,"HH:mm",out var time)&&TimeOnly.FromDateTime(now)>=time);
    internal static bool InputIsIdle(){var input=new Input{Size=8};return GetLastInputInfo(ref input)&&unchecked((uint)Environment.TickCount-input.Tick)>=180000;}
    internal bool Allowed()
    {
        var i=new Input{Size=8};if(!GetLastInputInfo(ref i)){Reason="无法读取空闲状态";return false;}
        long now=Stopwatch.GetTimestamp();bool cpu=false;
        if(GetSystemTimes(out var idle,out var kernel,out var user)){
            long total=kernel-lastKernel+user-lastUser;
            cpu=lastKernel!=0&&total>0&&1.0-(idle-lastIdle)/(double)total<.2;
            lastIdle=idle;lastKernel=kernel;lastUser=user;
        }
        if(!cpu||lastSample==0||Stopwatch.GetElapsedTime(lastSample,now).TotalSeconds>10)lowSince=now;
        lastSample=now;
        bool saver=!GetSystemPowerStatus(out var power)||power.Saver!=0;
        bool presentation=SHQueryUserNotificationState(out var state)!=0||state!=5;
        uint inactive=unchecked((uint)Environment.TickCount-i.Tick);
        Reason=inactive<180000?"等待3分钟无键鼠操作":saver?"节电模式，等待":presentation?"全屏或演示中，等待":Stopwatch.GetElapsedTime(lowSince,now).TotalSeconds<30?"等待CPU持续低于20%":"空闲条件已满足";
        return CanRun(inactive,Stopwatch.GetElapsedTime(lowSince,now).TotalSeconds,saver,presentation);
    }
}
