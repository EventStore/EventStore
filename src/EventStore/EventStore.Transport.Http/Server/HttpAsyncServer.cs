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
    public sealed class HttpAsyncServer
    {
        private static readonly ILogger Logger = LogManager.GetLoggerFor<HttpAsyncServer>();

        public event Action<HttpAsyncServer, HttpListenerContext> RequestReceived;
        
        public bool IsListening { get { return _listener.IsListening; } }
        public readonly string[] ListenPrefixes;

        private readonly HttpListener _listener;


        public HttpAsyncServer(string[] prefixes)
        {
            Ensure.NotNull(prefixes, "prefixes");

            ListenPrefixes = prefixes;

            _listener = new HttpListener();
            _listener.Realm = "ES";
            _listener.AuthenticationSchemes = AuthenticationSchemes.Basic | AuthenticationSchemes.Anonymous;
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
                var counter = 10;
                while (_listener.IsListening && counter-- > 0)
                {
                    _listener.Abort();
                    _listener.Stop();
                    _listener.Close();
                    if (_listener.IsListening)
                        System.Threading.Thread.Sleep(50);
                }
            }
            catch (ObjectDisposedException)
            {
                // that's ok
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
                // that's not application-level error, ignore and continue
            }
			catch (ObjectDisposedException e)
			{
				// that's ok, just continue
			}
            catch (InvalidOperationException)
            {
            }
            catch (Exception e)
            {
                Logger.DebugException(e, "EndGetContext exception. Status : {0}.", IsListening ? "listening" : "stopped");
            }

            if (success)
                ProcessRequest(context);

            try
            {
                _listener.BeginGetContext(ContextAcquired, null);
            }
            catch (HttpListenerException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (ApplicationException)
            {
            }
            catch (Exception e)
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
            var r = System.Linq.Expressions.Expression.Parameter(typeof (HttpListenerRequest), "r");
            var piHttpListenerContext = typeof (HttpListenerRequest).GetProperty("HttpListenerContext",
                                                                                 System.Reflection.BindingFlags.GetProperty
                                                                                 | System.Reflection.BindingFlags.NonPublic
                                                                                 | System.Reflection.BindingFlags.FlattenHierarchy
                                                                                 | System.Reflection.BindingFlags.Instance);
            var fiContext = typeof (HttpListenerRequest).GetField("context",
                                                                  System.Reflection.BindingFlags.GetProperty
                                                                  | System.Reflection.BindingFlags.NonPublic
                                                                  | System.Reflection.BindingFlags.FlattenHierarchy
                                                                  | System.Reflection.BindingFlags.Instance);
            var body = piHttpListenerContext != null
                           ? System.Linq.Expressions.Expression.Property(r, piHttpListenerContext)
                           : System.Linq.Expressions.Expression.Field(r, fiContext);
            var debugExpression = System.Linq.Expressions.Expression.Lambda<Func<HttpListenerRequest, HttpListenerContext>>(body, r);
            return debugExpression.Compile();
        }
#endif

    }
}