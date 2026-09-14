using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace QuietDesk.Reading;

internal static class SourceVerification
{
    internal static async Task Checks(string directory,List<string> lines)
    {
        void Check(bool ok,string name){if(!ok)throw new Exception(name);lines.Add("PASS "+name);}
        var handler=new FeedHandler();
        using var service=new ReadingService(directory,new ReadingSettings(),start:false,sourceHandler:handler);
        foreach(var s in service.Sources()){s.Subscribed=false;service.SaveSource(s);}
        var source=service.Sources().Single(s=>s.Id=="nchem");
        handler.Code=HttpStatusCode.ServiceUnavailable;
        await service.CheckSource(source.Id);
        source=service.Sources().Single(s=>s.Id==source.Id);
        Check(source.Failures==1&&source.NextAttempt>DateTimeOffset.Now&&!source.Subscribed,"single source failure retains unsubscribed choice and sets retry state");
        source.Subscribed=true;service.SaveSource(source);
        int before=handler.Calls;await service.Refresh();
        Check(handler.Calls==before&&service.Status.Contains("等待重试 1"),"automatic refresh reports sources skipped for backoff");
        handler.Code=HttpStatusCode.OK;await service.Refresh(true);
        var recovered=service.Sources().Single(s=>s.Id==source.Id);
        Check(handler.Calls==before+1&&recovered.Failures==0&&recovered.NextAttempt==null&&recovered.LastSuccess.HasValue&&recovered.ArticleCount==1,"manual refresh bypasses source backoff and replaces failure after success");
        source.Subscribed=false;service.SaveSource(source);
        recovered=service.Sources().Single(s=>s.Id==source.Id);
        Check(recovered.Failures==0&&recovered.LastSuccess.HasValue&&!recovered.Subscribed,"stale subscription checkbox does not overwrite fresh connection result");
        handler.Code=HttpStatusCode.NotModified;await service.CheckSource(source.Id);
        recovered=service.Sources().Single(s=>s.Id==source.Id);
        Check(recovered.Status.Contains("304")&&recovered.ArticleCount==1&&!recovered.Subscribed,"single source check works when unsubscribed and treats 304 as successful check");
        handler.Delay=new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending=service.CheckSource(source.Id);before=handler.Calls;
        Check(service.IsCheckingSource(source.Id),"in-flight source check exposes busy state");
        await service.CheckSource(source.Id);
        Check(handler.Calls==before,"duplicate single-source check does not send another request");
        handler.Delay.SetResult();await pending;
        Check(!service.IsCheckingSource(source.Id)&&service.Jobs().Count==0,"source-only check clears busy state without creating AI jobs");
    }

    private sealed class FeedHandler:HttpMessageHandler
    {
        internal HttpStatusCode Code=HttpStatusCode.OK;
        internal int Calls;
        internal TaskCompletionSource? Delay;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Calls++;if(Delay!=null)await Delay.Task.WaitAsync(ct);
            var response=new HttpResponseMessage(Code){Content=new StringContent("<rss><channel><item><title>Test article</title><link>https://www.nature.com/articles/test</link><description>A recorded abstract.</description></item></channel></rss>")};
            response.Headers.ETag=new System.Net.Http.Headers.EntityTagHeaderValue("\"fixture\"");return response;
        }
    }
}
