using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI.Transport.Http
{
    internal class HttpAsyncClient
    {
        private static readonly UTF8Encoding UTF8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        static HttpAsyncClient()
        {
            ServicePointManager.MaxServicePointIdleTime = 10000;
            ServicePointManager.DefaultConnectionLimit = 800;
        }

        private readonly ILogger _log;

        public HttpAsyncClient(ILogger log)
        {
            Ensure.NotNull(log, "log");
            _log = log;
        }

        //TODO GFY
        //this is a really really stupid way of doing this and it only works properly if
        //the moons align correctly in the 7th slot of jupiter on a tuesday when mercury
        //is rising. However it sort of works right now (unless you have proxies/dns/other
        //problems. The easy solution is to use httpclient from portable libraries but
        //it is currently limited in license to windows only.

        private void TimeoutCallback(object state, bool timedOut)
        {
            if(timedOut)
            {
                var req = state as HttpWebRequest;
                if(req != null)
                {
                    req.Abort();
                }
            }
        }

        public void Get(string url, UserCredentials userCredentials, TimeSpan timeout,
                        Action<HttpResponse> onSuccess, Action<Exception> onException)
        {
            Ensure.NotNull(url, "url");
            Ensure.NotNull(onSuccess, "onSuccess");
            Ensure.NotNull(onException, "onException");

            Receive(HttpMethod.Get, url, userCredentials, timeout, onSuccess, onException);
        }

        public void Post(string url, string body, string contentType, TimeSpan timeout, UserCredentials userCredentials,
                         Action<HttpResponse> onSuccess, Action<Exception> onException)
        {
            Ensure.NotNull(url, "url");
            Ensure.NotNull(body, "body");
            Ensure.NotNull(contentType, "contentType");
            Ensure.NotNull(onSuccess, "onSuccess");
            Ensure.NotNull(onException, "onException");

            Send(HttpMethod.Post, url, body, contentType, userCredentials, timeout, onSuccess, onException);
        }

        public void Delete(string url, UserCredentials userCredentials, TimeSpan timeout,
                           Action<HttpResponse> onSuccess, Action<Exception> onException)
        {
            Ensure.NotNull(url, "url");
            Ensure.NotNull(onSuccess, "onSuccess");
            Ensure.NotNull(onException, "onException");

            Receive(HttpMethod.Delete, url, userCredentials, timeout, onSuccess, onException);
        }

        public void Put(string url, string body, string contentType, UserCredentials userCredentials, TimeSpan timeout,
                        Action<HttpResponse> onSuccess, Action<Exception> onException)
        {
            Ensure.NotNull(url, "url");
            Ensure.NotNull(body, "body");
            Ensure.NotNull(contentType, "contentType");
            Ensure.NotNull(onSuccess, "onSuccess");
            Ensure.NotNull(onException, "onException");

            Send(HttpMethod.Put, url, body, contentType, userCredentials, timeout, onSuccess, onException);
        }

        private void Receive(string method, string url, UserCredentials userCredentials, TimeSpan timeout, 
                             Action<HttpResponse> onSuccess, Action<Exception> onException)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = method;
#if __MonoCS__
            request.KeepAlive = false;
            request.Pipelined = false;
#else
            request.KeepAlive = true;
            request.Pipelined = true;
#endif
            if (userCredentials != null)
                AddAuthenticationHeader(request, userCredentials);

            var result = request.BeginGetResponse(ResponseAcquired, new ClientOperationState(_log, request, onSuccess, onException));
            ThreadPool.RegisterWaitForSingleObject(result.AsyncWaitHandle, TimeoutCallback, request,
                                       (int)timeout.TotalMilliseconds, true);
        }

        private void Send(string method, string url, string body, string contentType, UserCredentials userCredentials, TimeSpan timeout,
                          Action<HttpResponse> onSuccess, Action<Exception> onException)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            var bodyBytes = UTF8NoBom.GetBytes(body);

            request.Method = method;
            request.KeepAlive = true;
            request.Pipelined = true;
            request.ContentLength = bodyBytes.Length;
            request.ContentType = contentType;
            if (userCredentials != null)
                AddAuthenticationHeader(request, userCredentials);

            var state = new ClientOperationState(_log, request, onSuccess, onException);
            state.InputStream = new MemoryStream(bodyBytes);

            var result = request.BeginGetRequestStream(GotRequestStream, state);
            ThreadPool.RegisterWaitForSingleObject(result.AsyncWaitHandle, TimeoutCallback, request,
                                                   (int) timeout.TotalMilliseconds, true);
        }

        private void AddAuthenticationHeader(HttpWebRequest request, UserCredentials userCredentials)
        {
            Ensure.NotNull(userCredentials, "userCredentials");
            var httpAuthentication = string.Format("{0}:{1}", userCredentials.Username, userCredentials.Password);
            var encodedCredentials = Convert.ToBase64String(Helper.UTF8NoBom.GetBytes(httpAuthentication));
            request.Headers.Add("Authorization", string.Format("Basic {0}", encodedCredentials));
        }

        private void ResponseAcquired(IAsyncResult ar)
        {
            var state = (ClientOperationState)ar.AsyncState;
            try
            {
                var response = (HttpWebResponse)state.Request.EndGetResponseExtended(ar);
                var networkStream = response.GetResponseStream();
                if (networkStream == null) throw new Exception("Response stream was null.");

                state.Response = new HttpResponse(response);
                state.InputStream = networkStream;
                state.OutputStream = new MemoryStream();

                var copier = new AsyncStreamCopier<ClientOperationState>(state.InputStream, state.OutputStream, state);
                copier.Completed += ResponseRead;
                copier.Start();
            }
            catch (Exception e)
            {
                state.Dispose();
                state.OnError(e);
            }
        }

        private void ResponseRead(object sender, EventArgs eventArgs)
        {
            var copier = (AsyncStreamCopier<ClientOperationState>)sender;
            var state = copier.AsyncState;

            if (copier.Error != null)
            {
                state.Dispose();
                state.OnError(copier.Error);
                return;
            }

            state.OutputStream.Seek(0, SeekOrigin.Begin);
            var memStream = (MemoryStream)state.OutputStream;
            state.Response.Body = UTF8NoBom.GetString(memStream.GetBuffer(), 0, (int)memStream.Length);

            state.Dispose();
            state.OnSuccess(state.Response);
        }

        private void GotRequestStream(IAsyncResult ar)
        {
            var state = (ClientOperationState)ar.AsyncState;
            try
            {
                var networkStream = state.Request.EndGetRequestStream(ar);
                state.OutputStream = networkStream;
                var copier = new AsyncStreamCopier<ClientOperationState>(state.InputStream, networkStream, state);
                copier.Completed += RequestWrote;
                copier.Start();
            }
            catch (Exception e)
            {
                state.Dispose();
                state.OnError(e);
            }
        }

        private void RequestWrote(object sender, EventArgs eventArgs)
        {
            var copier = (AsyncStreamCopier<ClientOperationState>)sender;
            var state = copier.AsyncState;
            var httpRequest = state.Request;

            if (copier.Error != null)
            {
                state.Dispose();
                state.OnError(copier.Error);
                return;
            }

            state.Dispose();
            httpRequest.BeginGetResponse(ResponseAcquired, state);
        }
    }
}