using System;
namespace QuietDesk;

// Stereo-linked peak protection. Below the ceiling samples remain bit-identical
// after a fixed 256-frame delay. No waveshaping or frequency filtering.
internal sealed class StereoPeakLimiter
{
    internal const int DelayFrames=256;
    private const float Ceiling=.98f;
    private readonly float[] delay=new float[DelayFrames*2];
    private readonly float[] peaks=new float[DelayFrames+2];
    private readonly long[] positions=new long[DelayFrames+2];
    private int read,head,size;private long frame;private float gain=1;
    private static readonly float Release=(float)Math.Exp(-1.0/(44100*.08));
    internal void Process(float[] samples,int offset,int count)
    {
        if(count%2!=0)throw new ArgumentException("Stereo samples must contain complete frames.");
        for(int i=offset;i<offset+count;i+=2,frame++){
            float left=float.IsFinite(samples[i])?samples[i]:0,right=float.IsFinite(samples[i+1])?samples[i+1]:0;
            long oldest=frame-DelayFrames;
            while(size>0&&positions[head]<oldest){head=(head+1)%peaks.Length;size--;}
            float peak=Math.Max(Math.Abs(left),Math.Abs(right));
            while(size>0&&peaks[(head+size-1)%peaks.Length]<=peak)size--;
            int tail=(head+size)%peaks.Length;peaks[tail]=peak;positions[tail]=frame;size++;
            float target=peaks[head]>Ceiling?Ceiling/peaks[head]:1;
            gain=target<gain?target:target+(gain-target)*Release;
            float previousLeft=delay[read],previousRight=delay[read+1];delay[read]=left;delay[read+1]=right;read=(read+2)%delay.Length;
            samples[i]=previousLeft*gain;samples[i+1]=previousRight*gain;
        }
    }
}
