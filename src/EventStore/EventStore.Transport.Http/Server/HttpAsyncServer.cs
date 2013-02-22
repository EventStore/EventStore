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
using System.Net;
using System.Text;
using EventStore.Common.Log;
using EventStore.Common.Utils;

namespace EventStore.Transport.Http.Server
{
    public class HttpAsyncServer
    {
        private static readonly ILogger Logger = LogManager.GetLoggerFor<HttpAsyncServer>();

        public event Action<HttpAsyncServer, HttpListenerContext> RequestReceived;

        public bool IsListening { get { return _listener.IsListening; } }
        public IEnumerable<string> ListenPrefixes { get { return _listener.Prefixes; } }

        private readonly HttpListener _listener;

        public HttpAsyncServer(string[] prefixes)
        {
            Ensure.NotNull(prefixes, "prefixes");

            _listener = new HttpListener();
            foreach (var prefix in prefixes)
            {
                _listener.Prefixes.Add(prefix);
            }
        }

        public bool TryStart()
        {
            var uriPrefixes = _listener.Prefixes;
            try
            {
                Logger.Info("Starting HTTP server on [{0}]...", string.Join(",", _listener.Prefixes));

                _listener.Start();
                _listener.BeginGetContext(ContextAcquired, null);

                Logger.Info("HTTP server is up and listening on [{0}]", string.Join(",", _listener.Prefixes));

                return true;
            }
            catch (HttpListenerException e)
            {
                var helpMessage = CreateHttpServerStartupErrorHelpMessage(uriPrefixes, e);
                var errorMessageWithHelp = String.Format("{1}{0}HttpListenerException: {2}{0}{3}", Environment.NewLine, HttpServerStartupErrorMessage, e.Message, helpMessage);
                Logger.FatalException(e, errorMessageWithHelp);
                return false;
            }
            catch (Exception e)
            {
                Logger.FatalException(e, HttpServerStartupErrorMessage);
                return false;
            }
        }

        private const string HttpServerStartupErrorMessage = "Failed to start HTTP server";
        private const int ErrorAccessDenied = 5; // Standard Windows API ERROR_ACCESS_DENIED code

        private static string CreateHttpServerStartupErrorHelpMessage(IEnumerable<string> uriPrefixes, HttpListenerException exception)
        {
            var helpMessage = new StringBuilder();
            if (exception.ErrorCode == ErrorAccessDenied)
            {
                var domain = Environment.UserDomainName;
                var user = Environment.UserName;
                helpMessage.AppendLine("You may need to grant the URLACL permissions to run the HTTP server.");
                helpMessage.AppendLine("From a Windows shell, use the \"netsh\" comand to grant these, e.g.:");
                foreach (var uriPrefix in uriPrefixes)
                {
                    var addUrlAclCommand = String.Format("  netsh http add urlacl {0} user={1}\\{2}",
                        uriPrefix, domain, user);
                    helpMessage.AppendLine(addUrlAclCommand);
                }
            }
            return helpMessage.ToString();
        }

        public void Shutdown()
        {
            try
            {
                _listener.Close();
            }
            catch (Exception e)
            {
                Logger.ErrorException(e, "Error while shutting down HTTP server");
            }
        }

        private void ContextAcquired(IAsyncResult ar)
        {
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

        protected virtual void OnRequestReceived(HttpListenerContext context)
        {
            var handler = RequestReceived;
            if (handler != null)
                handler(this, context);
        }
    }
}