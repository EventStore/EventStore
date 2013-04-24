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
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using EventStore.Common.Log;
using EventStore.Common.Utils;

namespace EventStore.Transport.Http.Server
{
    public sealed class HttpAsyncServer
    {
        private static readonly ILogger Logger = LogManager.GetLoggerFor<HttpAsyncServer>();

        public event Action<HttpAsyncServer, HttpListenerContext> RequestReceived;
        
        public bool IsListening { get { return _listener.IsListening; } }
        public readonly string[] ListenPrefixes;

        private readonly HttpListener _listener;

#if __MonoCS__
        private static readonly Func<HttpListenerRequest, HttpListenerContext> _getContext = CreateGetContext();
#endif

        public HttpAsyncServer(string[] prefixes)
        {
            Ensure.NotNull(prefixes, "prefixes");

            ListenPrefixes = prefixes;

            _listener = new HttpListener();
            _listener.Realm = "ES";
#if __MonoCS__
                _listener.AuthenticationSchemeSelectorDelegate =
                    request =>
                    _getContext(request).Response.StatusCode == HttpStatusCode.Unauthorized
                        ? AuthenticationSchemes.Basic
                        : AuthenticationSchemes.Anonymous;
#else
                _listener.AuthenticationSchemes = AuthenticationSchemes.Basic | AuthenticationSchemes.Anonymous;
#endif
            foreach (var prefix in prefixes)
            {
                _listener.Prefixes.Add(prefix);
            }
        }

        public bool TryStart()
        {
            try
            {
                Logger.Info("Starting HTTP server on [{0}]...", string.Join(",", _listener.Prefixes));
                _listener.Start();

                _listener.BeginGetContext(ContextAcquired, null);

                Logger.Info("HTTP server is up and listening on [{0}]", string.Join(",", _listener.Prefixes));

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
                Logger.ErrorException(e, "Error while shutting down http server");
            }
        }

        private void ContextAcquired(IAsyncResult ar)
        {
            if (!IsListening)
                return;

            HttpListenerContext context = null;
            bool success = false;
            try
            {
                context = _listener.EndGetContext(ar);
                success = true;
            }
            catch (HttpListenerException e)
            {
                Logger.ErrorException(e, "EndGetContext error. Status : {0}.", IsListening ? "listening" : "stopped");
            }
            catch (InvalidOperationException e)
            {
                Logger.ErrorException(e, "EndGetContext error. Status : {0}.", IsListening ? "listening" : "stopped");
            }

            if (success)
                ProcessRequest(context);

            try
            {
                _listener.BeginGetContext(ContextAcquired, null);
            }
            catch (HttpListenerException e)
            {
                Logger.ErrorException(e, "BeginGetContext error. Status : {0}.", IsListening ? "listening" : "stopped");
            }
            catch (InvalidOperationException e)
            {
                Logger.ErrorException(e, "BeginGetContext error. Status : {0}.", IsListening ? "listening" : "stopped");
            }
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            context.Response.StatusCode = HttpStatusCode.InternalServerError;
            OnRequestReceived(context);
        }

        private void OnRequestReceived(HttpListenerContext context)
        {
            var handler = RequestReceived;
            if (handler != null)
                handler(this, context);
        }

 #if __MonoCS__
       private static Func<HttpListenerRequest, HttpListenerContext> CreateGetContext()
        {
            var r = Expression.Parameter(typeof (HttpListenerRequest), "r");
            var piHttpListenerContext = typeof (HttpListenerRequest).GetProperty(
                "HttpListenerContext",
                BindingFlags.GetProperty | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy| BindingFlags.Instance);
            var fiContext = typeof (HttpListenerRequest).GetField(
                "context",
                BindingFlags.GetProperty | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy| BindingFlags.Instance);
            var body = piHttpListenerContext != null
                           ? Expression.Property(r, piHttpListenerContext)
                           : Expression.Field(r, fiContext);
            var debugExpression = Expression.Lambda<Func<HttpListenerRequest, HttpListenerContext>>(body, r);
            return debugExpression.Compile();
        }
#endif

    }
}