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
            using(var direct=new MixBus("unused"))using(var bridged=new MixBus("unused")){
                direct.Master=bridged.Master=1;direct.MediaLevel=bridged.MediaLevel=.2f;direct.SetMedia(new Tone());bridged.SetMedia(new Tone());
                var adapter=new NAudio.Wave.SampleProviders.SampleToWaveProvider(bridged);var bytes=new byte[8192];var floats=new float[2048];double maxError=0;
                for(int block=0;block<100;block++){direct.Read(floats,0,floats.Length);adapter.Read(bytes,0,bytes.Length);for(int i=0;i<floats.Length;i++)maxError=Math.Max(maxError,Math.Abs(floats[i]-BitConverter.ToSingle(bytes,i*4)));}
                Check(maxError<.000001,"WASAPI byte/float bridge differs from direct mixer: max error "+maxError);
                lines.Add("PASS actual byte/float output adapter matches direct mixer across 100 reused buffers");
            }
            var old=new float[44100];for(int i=0;i<old.Length;i++){float x=(float)(.8*Math.Sin(2*Math.PI*1000*i/44100));old[i]=x/(1+Math.Abs(x));}lines.Add($"REFERENCE previous waveshaping formula: 1 kHz THD+N {Distortion(old):F6}%");
            float[] RenderPair(float master,float mediaLevel){using var pair=new MixBus(Path.Combine(AppContext.BaseDirectory,"Assets","Sounds"));pair.Master=master;pair.MediaLevel=mediaLevel;pair.SetMedia(new Tone());var values=new float[44100];var block=new float[2048];int done=0;while(done<88200){int frames=Math.Min(1024,88200-done);pair.Read(block,0,frames*2);for(int i=0;i<frames;i++)if(done+i>=44100)values[done+i-44100]=block[i*2];done+=frames;}return values;}
            var lowMaster=RenderPair(.2f,1f);var highMaster=RenderPair(1f,.2f);double difference=0,energy=0;for(int i=0;i<lowMaster.Length;i++){difference+=Math.Pow(lowMaster[i]-highMaster[i],2);energy+=lowMaster[i]*lowMaster[i];}double relative=Math.Sqrt(difference/energy)*100;Check(relative<.05,"Equal-product master/media controls produce different signals");lines.Add($"PASS master/media 20%/100% vs 100%/20%: relative RMS difference {relative:F6}%");
            using(var captureBus=new MixBus("unused")){
                captureBus.Master=1;captureBus.MediaLevel=.2f;captureBus.SetMedia(new Tone());var task=captureBus.CaptureOutput();
                var adapter=new NAudio.Wave.SampleProviders.SampleToWaveProvider(captureBus);var bytes=new byte[8192];var expected=new float[44100*2*5];int position=0;
                while(position<expected.Length){adapter.Read(bytes,0,bytes.Length);int n=Math.Min(2048,expected.Length-position);for(int i=0;i<n;i++)expected[position+i]=BitConverter.ToSingle(bytes,i*4);position+=n;}
                Check(task.GetAwaiter().GetResult().SequenceEqual(expected),"Capture converts aliased storage bytes instead of copying floats");
                captureBus.SetMedia(null);for(int i=0;i<4;i++)adapter.Read(bytes,0,bytes.Length);
                Check(bytes.All(x=>x==0),"Reused output buffer retains audio after source removal");
                var ring=new FloatRing(WaveFormat.CreateIeeeFloatWaveFormat(44100,2));var ringAdapter=new NAudio.Wave.SampleProviders.SampleToWaveProvider(ring);ring.Write(new float[]{.2f,-.2f},0,2);ringAdapter.Read(bytes,0,bytes.Length);ringAdapter.Read(bytes,0,bytes.Length);Check(bytes.All(x=>x==0),"Ring underflow retains output bytes");
                lines.Add("PASS aliased PCM capture is exact; source removal and ring underflow clear all reused output bytes");
            }
            using(var recordingBus=new MixBus("unused")){
                recordingBus.Master=.8f;recordingBus.MediaLevel=.4f;recordingBus.SetMedia(new Tone());
                var task=recordingBus.CaptureOutput();var expected=new float[44100*2*5];var block=new float[2052];int position=0;
                while(position<expected.Length){recordingBus.Read(block,4,2048);int n=Math.Min(2048,expected.Length-position);Array.Copy(block,4,expected,position,n);position+=n;}
                Check(task.GetAwaiter().GetResult().SequenceEqual(expected),"Captured PCM differs from mixer output");
                var pending=recordingBus.CaptureOutput();recordingBus.Dispose();
                try{pending.GetAwaiter().GetResult();throw new Exception("Pending capture was not canceled on disposal");}catch(OperationCanceledException){}
                lines.Add("PASS five-second bounded capture matches final PCM with nonzero offsets; pending capture cancels on disposal");
            }
            File.WriteAllLines(output,lines);return 0;
        }catch(Exception e){lines.Add("FAIL "+e);File.WriteAllLines(output,lines);return 1;}
    }
}
