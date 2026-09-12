using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Threading;
namespace QuietDesk;

// Explicit diagnostic launch only. At most twelve five-second application PCM clips.
internal sealed class LocalAudioDiagnostic : IDisposable
{
    private readonly PlayerModel model;
    private readonly string directory;
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(2)};
    private string? lastKey;
    private bool busy,disposed;
    private int captures;
    internal LocalAudioDiagnostic(PlayerModel model,string directory)
    {
        this.model=model;this.directory=directory;Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,"session.txt"),$"Started {DateTimeOffset.Now:O}\nData: {model.DataDirectory}\nApplication PCM only; maximum 12 clips.\n");
        model.PropertyChanged+=Changed;
        foreach(var channel in model.Channels)channel.PropertyChanged+=Changed;
        timer.Tick+=Tick;timer.Start();
    }
    private void Changed(object? sender,PropertyChangedEventArgs e)
    {
        if(sender==model&&e.PropertyName is not ("Master" or "MediaVolume" or "Playing" or "MediaTitle" or "MediaPaused"))return;
        if(!disposed&&captures<12){timer.Stop();timer.Start();}
    }
    private async void Tick(object? sender,EventArgs e)
    {
        if(busy||!model.Playing||model.Loading||disposed)return;
        string key=$"{model.Master:R}/{model.MediaVolume:R}/{model.MediaTitle}/{model.MediaPaused}/"+string.Join(";",model.Channels.Select(c=>$"{c.Info.Id}:{c.Enabled}:{c.Volume:R}"));
        if(key==lastKey)return;
        lastKey=key;busy=true;
        try{var result=await model.ExportAudioDiagnostic(directory);File.AppendAllText(Path.Combine(directory,"session.txt"),$"{DateTimeOffset.Now:O} {result??model.Status}\n");}
        catch(Exception error){File.AppendAllText(Path.Combine(directory,"session.txt"),error.Message+"\n");}
        finally{busy=false;if(++captures>=12)timer.Stop();}
    }
    public void Dispose(){disposed=true;timer.Stop();timer.Tick-=Tick;model.PropertyChanged-=Changed;foreach(var channel in model.Channels)channel.PropertyChanged-=Changed;}
}
