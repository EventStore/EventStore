using System;
using System.IO;
using System.Net;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI.Transport.Http
{
    internal class ClientOperationState: IDisposable
    {
        public readonly HttpWebRequest Request;
        public readonly Action<HttpResponse> OnSuccess;
        public readonly Action<Exception> OnError;

        public HttpResponse Response { get; set; }

        public Stream InputStream { get; set; }
        public Stream OutputStream { get; set; }

        private readonly ILogger _log;

        public ClientOperationState(ILogger log, HttpWebRequest request, Action<HttpResponse> onSuccess, Action<Exception> onError)
        {
            Ensure.NotNull(log, "log");
            Ensure.NotNull(request, "request");
            Ensure.NotNull(onSuccess, "onSuccess");
            Ensure.NotNull(onError, "onError");

            _log = log;
            
            Request = request;
            OnSuccess = onSuccess;
            OnError = onError;
        }

        public void Dispose()
        {
            SafelyDispose(InputStream, OutputStream);
        }

        private void SafelyDispose(params Stream[] streams)
        {
            if (streams == null || streams.Length == 0)
                return;

            foreach (var stream in streams)
            {
                try
                {
                    if (stream != null)
                        stream.Dispose();
                }
                catch (Exception e)
                {
                    //Exceptions may be thrown when client shutdown and we were unable to write all the data,
                    //Nothing we can do, ignore (another option - globally ignore write errors)
                    _log.Debug("Error while closing stream : {0}", e.Message);
                }
            }
        }
    }
}