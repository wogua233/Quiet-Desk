using System;
using System.Threading.Tasks;
namespace QuietDesk;
// Opt-in, bounded five-second capture of this application's final PCM only.
internal sealed class OutputCapture
{
    private readonly float[] samples=new float[44100*2*5];private int written;
    internal readonly TaskCompletionSource<float[]> Completion=new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal void Append(float[] source,int offset,int count){if(written==samples.Length)return;int n=Math.Min(count,samples.Length-written);source.AsSpan(offset,n).CopyTo(samples.AsSpan(written,n));written+=n;if(written==samples.Length)Completion.TrySetResult(samples);}
}
