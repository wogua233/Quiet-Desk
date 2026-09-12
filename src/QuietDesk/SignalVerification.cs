using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NAudio.Wave;
namespace QuietDesk;
internal static class SignalVerification
{
    private sealed class Tone:ISampleProvider
    {
        private long frame;public WaveFormat WaveFormat=>WaveFormat.CreateIeeeFloatWaveFormat(44100,2);
        public int Read(float[] b,int o,int c){for(int i=0;i<c;i+=2,frame++){float x=(float)(.8*Math.Sin(2*Math.PI*1000*frame/44100));b[o+i]=b[o+i+1]=x;}return c;}
    }
    private static double Distortion(float[] samples){int n=samples.Length;double a=0,b=0;for(int i=0;i<n;i++){double t=2*Math.PI*1000*i/44100;a+=samples[i]*Math.Sin(t);b+=samples[i]*Math.Cos(t);}a*=2.0/n;b*=2.0/n;double residual=0;for(int i=0;i<n;i++){double t=2*Math.PI*1000*i/44100,e=samples[i]-a*Math.Sin(t)-b*Math.Cos(t);residual+=e*e;}return Math.Sqrt(residual/(n*(a*a+b*b)/2))*100;}
    private static float[] Process(float[] source,int block){var b=(float[])source.Clone();var limiter=new StereoPeakLimiter();for(int i=0;i<b.Length;i+=block)limiter.Process(b,i,Math.Min(block,b.Length-i));return b;}
    private static void Check(bool condition,string error){if(!condition)throw new Exception(error);}
    internal static int Run(string[] args){var lines=new List<string>();int oi=Array.IndexOf(args,"--out");string output=oi>=0?args[oi+1]:"signal-test.txt";
        try{
            var normal=new float[44100*2];for(int i=0;i<44100;i++){normal[i*2]=(float)(.8*Math.Sin(i*.19));normal[i*2+1]=(float)(.4*Math.Cos(i*.31));}
            var transparent=Process(normal,1024);for(int i=StereoPeakLimiter.DelayFrames*2;i<normal.Length;i++)Check(transparent[i]==normal[i-StereoPeakLimiter.DelayFrames*2],"Normal-range signal changed");lines.Add("PASS normal-range stereo signal is bit-identical after 256-frame delay");
            var loud=new float[44100*4];for(int i=0;i<loud.Length/2;i++){loud[i*2]=(float)(3*Math.Sin(i*.17));if(i%701==0)loud[i*2]=8;loud[i*2+1]=loud[i*2]*.5f;}
            var protectedSignal=Process(loud,1024);Check(protectedSignal.All(x=>float.IsFinite(x)&&Math.Abs(x)<=.98001f),"Overload exceeds ceiling");for(int i=0;i<protectedSignal.Length;i+=2)Check(Math.Abs(protectedSignal[i+1]-protectedSignal[i]*.5f)<.000001f,"Stereo image changed");Check(protectedSignal.SequenceEqual(Process(loud,258)),"Buffer boundaries change signal");lines.Add("PASS overload and transient peaks bounded to 0.98, stereo-linked, block-size invariant");
            Check(Process(new float[44100],512).All(x=>x==0),"Silence produced noise");lines.Add("PASS digital silence remains exactly zero");
            using var bus=new MixBus(Path.Combine(AppContext.BaseDirectory,"Assets","Sounds"));bus.Master=1;bus.MediaLevel=1;bus.SetMedia(new Tone());var captured=new float[44100];var scratch=new float[2056];int rendered=0;
            while(rendered<88200){int frames=Math.Min(1024,88200-rendered);bus.Read(scratch,4,frames*2);for(int i=0;i<frames;i++)if(rendered+i>=44100)captured[rendered+i-44100]=scratch[4+i*2];rendered+=frames;}
            double thd=Distortion(captured);Check(thd<.01,"Full-volume mixer sine distortion: "+thd);lines.Add($"PASS full mixer at master=100%, media=100%: 1 kHz THD+N {thd:F6}%");
            var old=new float[44100];for(int i=0;i<old.Length;i++){float x=(float)(.8*Math.Sin(2*Math.PI*1000*i/44100));old[i]=x/(1+Math.Abs(x));}lines.Add($"REFERENCE previous waveshaping formula: 1 kHz THD+N {Distortion(old):F6}%");
            File.WriteAllLines(output,lines);return 0;
        }catch(Exception e){lines.Add("FAIL "+e);File.WriteAllLines(output,lines);return 1;}
    }
}
