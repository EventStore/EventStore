// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using EventStore.Common.Utils;

namespace EventStore.Transport.Http.Client
{
    public class HttpAsyncClient : IHttpClient
    {
        static HttpAsyncClient()
        {
            ServicePointManager.MaxServicePointIdleTime = 10000;
            ServicePointManager.DefaultConnectionLimit = 500;
        }

        public void Get(string url, Action<HttpResponse> onSuccess, Action<Exception> onException)
        {
            Ensure.NotNull(url, "url");
            Ensure.NotNull(onSuccess, "onSuccess");
            Ensure.NotNull(onException, "onException");

            Receive(HttpMethod.Get, url, null, onSuccess, onException);
        }

        public void Get(string url, IEnumerable<KeyValuePair<string,string>> headers, Action<HttpResponse> onSuccess, Action<Exception> onException)
        {
            Ensure.NotNull(url, "url");
            Ensure.NotNull(onSuccess, "onSuccess");
            Ensure.NotNull(onException, "onException");

            Receive(HttpMethod.Get, url, headers, onSuccess, onException);
        }
        public void Post(string url, string body, string contentType, Action<HttpResponse> onSuccess, Action<Exception> onException)
        {
            Post(url, body, contentType, null, onSuccess, onException);
        }

        public void Post(string url, string body, string contentType, IEnumerable<KeyValuePair<string,string>> headers, 
                         Action<HttpResponse> onSuccess, Action<Exception> onException)
        {
            Ensure.NotNull(url, "url");
            Ensure.NotNull(body, "body");
            Ensure.NotNull(contentType, "contentType");
            Ensure.NotNull(onSuccess, "onSuccess");
            Ensure.NotNull(onException, "onException");

            Send(HttpMethod.Post, url, body, contentType, headers, onSuccess, onException);
        }

        public void Delete(string url, Action<HttpResponse> onSuccess, Action<Exception> onException)
        {
            Ensure.NotNull(url, "url");
            Ensure.NotNull(onSuccess, "onSuccess");
            Ensure.NotNull(onException, "onException");

            Receive(HttpMethod.Delete, url, null, onSuccess, onException);
        }

        public void Put(string url, string body, string contentType, Action<HttpResponse> onSuccess, Action<Exception> onException)
        {
            Ensure.NotNull(url, "url");
            Ensure.NotNull(body, "body");
            Ensure.NotNull(contentType, "contentType");
            Ensure.NotNull(onSuccess, "onSuccess");
            Ensure.NotNull(onException, "onException");

            Send(HttpMethod.Put, url, body, contentType, null, onSuccess, onException);
        }

        private void Receive(string method, string url, IEnumerable<KeyValuePair<string, string>> headers,
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
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
            }

            request.BeginGetResponse(ResponseAcquired, new ClientOperationState(request, onSuccess, onException));
        }

        private void Send(string method, string url, string body, string contentType, 
                          IEnumerable<KeyValuePair<string, string>> headers, 
                          Action<HttpResponse> onSuccess, Action<Exception> onException)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            var bodyBytes = Helper.UTF8NoBom.GetBytes(body);

            request.Method = method;
            request.KeepAlive = true;
            request.Pipelined = true;
            request.ContentLength = bodyBytes.Length;
            request.ContentType = contentType;

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
            }

            var state = new ClientOperationState(request, onSuccess, onException)
            {
                InputStream = new MemoryStream(bodyBytes)
            };
            request.BeginGetRequestStream(GotRequestStream, state);
        }

        private void ResponseAcquired(IAsyncResult ar)
        {
            var state = (ClientOperationState)ar.AsyncState;
            try
            {
                var response = (HttpWebResponse)state.Request.EndGetResponseExtended(ar);
                var networkStream = response.GetResponseStream();

                if (networkStream == null)
                    throw new ArgumentNullException("networkStream", "Response stream was null");

                state.Response = new HttpResponse(response);
                state.InputStream = networkStream;
                state.OutputStream = new MemoryStream();

                var copier = new AsyncStreamCopier<ClientOperationState>(state.InputStream, state.OutputStream, state, ResponseRead);
                copier.Start();
            }
            catch (Exception e)
            {
                state.DisposeIOStreams();
                state.OnError(e);
            }
        }

        private void ResponseRead(AsyncStreamCopier<ClientOperationState> copier)
        {
            var state = copier.AsyncState;

            if (copier.Error != null)
            {
                state.DisposeIOStreams();
                state.OnError(copier.Error);
                return;
            }

            state.OutputStream.Seek(0, SeekOrigin.Begin);
            var memStream = (MemoryStream)state.OutputStream;
            state.Response.Body = Helper.UTF8NoBom.GetString(memStream.GetBuffer(), 0, (int) memStream.Length);

            state.DisposeIOStreams();
            state.OnSuccess(state.Response);
        }

        private void GotRequestStream(IAsyncResult ar)
        {
            var state = (ClientOperationState) ar.AsyncState;
            try
            {
                var networkStream = state.Request.EndGetRequestStream(ar);
                state.OutputStream = networkStream;
                var copier = new AsyncStreamCopier<ClientOperationState>(state.InputStream, networkStream, state, RequestWrote);
                copier.Start();
            }
            catch (Exception e)
            {
                state.DisposeIOStreams();
                state.OnError(e);
            }
        }

        private void RequestWrote(AsyncStreamCopier<ClientOperationState> copier)
        {
            var state = copier.AsyncState;
            var httpRequest = state.Request;

            if (copier.Error != null)
            {
                state.DisposeIOStreams();
                state.OnError(copier.Error);
                return;
            }

            state.DisposeIOStreams();
            httpRequest.BeginGetResponse(ResponseAcquired, state);
        }
    }
}