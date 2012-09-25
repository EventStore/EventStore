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
using System.Net;
using EventStore.Common.Log;
using EventStore.Common.Utils;

namespace EventStore.Transport.Http.Server
{
    public class HttpAsyncServer
    {
        private static readonly ILogger Logger = LogManager.GetLoggerFor<HttpAsyncServer>();

        public event Action<HttpAsyncServer, HttpListenerContext> RequestReceived;
        public bool IsListening
        {
            get
            {
                return _listener != null && _listener.IsListening;
            }
        }

        private readonly HttpListener _listener;

        public HttpAsyncServer(string[] prefixes)
        {
            Ensure.NotNull(prefixes, "prefixes");

            _listener = new HttpListener();
            foreach (var prefix in prefixes)
                _listener.Prefixes.Add(prefix);
        }

        public bool TryStart()
        {
            try
            {
                Logger.Info("Starting http server on [{0}]...", string.Join(",", _listener.Prefixes));

                _listener.Start();
                _listener.BeginGetContext(ContextAcquired, null);

                Logger.Info("http up and listening on [{0}]", string.Join(",", _listener.Prefixes));

                return true;
            }
            catch (Exception e)
            {
                Logger.FatalException(e, "Failed to start http server");
                return false;
            }
        }

        public void Shutdown()
        {
            try
            {
                _listener.Close();
            }
            catch (Exception e)
            {
                Logger.FatalException(e, "Error while shutting down http server");
            }
        }

        private void ContextAcquired(IAsyncResult ar)
        {
            HttpListenerContext context;
            try
            {
                context = _listener.EndGetContext(ar);
                _listener.BeginGetContext(ContextAcquired, null);
            }
            catch (HttpListenerException e)
            {
                Logger.ErrorException(e, "EndGetContext/BeginGetContext error. Status : {0}",
                                      IsListening ? "listening" : "stopped");
                return;
            }
            catch (InvalidOperationException e)
            {
                Logger.ErrorException(e, "EndGetContext/BeginGetContext error. Status : {0}",
                                      IsListening ? "listening" : "stopped");
                return;
            }

            ProcessRequest(context);
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            context.Response.StatusCode = HttpStatusCode.InternalServerError;
            OnRequestReceived(context);
        }

        protected virtual void OnRequestReceived(HttpListenerContext context)
        {
            var handler = RequestReceived;
            if (handler != null)
                handler(this, context);
        }
    }
}