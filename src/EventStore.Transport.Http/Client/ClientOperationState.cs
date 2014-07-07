using System;
using System.IO;
using System.Net;
using EventStore.Common.Utils;

namespace EventStore.Transport.Http.Client
{
    public class ClientOperationState
    {
        public readonly HttpWebRequest Request;
        public readonly Action<HttpResponse> OnSuccess;
        public readonly Action<Exception> OnError;

        public HttpResponse Response { get; set; }

        public Stream InputStream { get; set; }
        public Stream OutputStream { get; set; }

        public ClientOperationState(HttpWebRequest request, Action<HttpResponse> onSuccess, Action<Exception> onError)
        {
            Ensure.NotNull(request, "request");
            Ensure.NotNull(onSuccess, "onSuccess");
            Ensure.NotNull(onError, "onError");

            Request = request;
            OnSuccess = onSuccess;
            OnError = onError;
        }

        public void DisposeIOStreams()
        {
            IOStreams.SafelyDispose(InputStream, OutputStream);
        }
    }
}