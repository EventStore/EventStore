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
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.Transport.Http.Authentication;
using EventStore.Core.Services.Transport.Http.Messages;
using EventStore.Transport.Http.EntityManagement;
using EventStore.Transport.Http.Server;

namespace EventStore.Core.Services.Transport.Http
{
    public class HttpService : IHttpService,
                               IHandle<SystemMessage.SystemInit>,
                               IHandle<SystemMessage.BecomeShuttingDown>,
                               IHandle<HttpMessage.SendOverHttp>,
                               IHandle<HttpMessage.PurgeTimedOutRequests>
    {
        private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

        public bool IsListening
        {
            get { return _server.IsListening; }
        }

        public IEnumerable<string> ListenPrefixes
        {
            get { return _server.ListenPrefixes; }
        }

        public ServiceAccessibility Accessibility
        {
            get { return _accessibility; }
        }

        private readonly ServiceAccessibility _accessibility;
        private readonly IPublisher _inputBus;
        private readonly IUriRouter _uriRouter;
        private readonly IEnvelope _publishEnvelope;

        private readonly HttpMessagePipe _httpPipe;
        private readonly HttpAsyncServer _server;
        private readonly MultiQueuedHandler _requestsMultiHandler;
        private readonly AuthenticationProvider[] _authenticationProviders;

        public HttpService(
            ServiceAccessibility accessibility, IPublisher inputBus, IUriRouter uriRouter,
            MultiQueuedHandler multiQueuedHandler, AuthenticationProvider[] authenticationProviders,
            params string[] prefixes)
        {
            Ensure.NotNull(inputBus, "inputBus");
            Ensure.NotNull(uriRouter, "uriRouter");
            Ensure.NotNull(prefixes, "prefixes");

            _accessibility = accessibility;
            _inputBus = inputBus;
            _uriRouter = uriRouter;
            _publishEnvelope = new PublishEnvelope(inputBus);

            _httpPipe = new HttpMessagePipe();

            _requestsMultiHandler = multiQueuedHandler;
            _authenticationProviders = authenticationProviders;

            _server = new HttpAsyncServer(prefixes);
            _server.RequestReceived += RequestReceived;
        }

        public void CreateAndSubscribePipeline(IBus bus)
        {
            var requestProcessor = new AuthenticatedHttpRequestProcessor(this);
            var requestAuthenticationManager = new IncomingHttpRequestAuthenticationManager(_authenticationProviders);

            bus.Subscribe<IncomingHttpRequestMessage>(requestAuthenticationManager);
            bus.Subscribe<AuthenticatedHttpRequestMessage>(requestProcessor);
            bus.Subscribe<HttpMessage.PurgeTimedOutRequests>(requestProcessor);
        }

        public void Handle(SystemMessage.SystemInit message)
        {
            if (_server.TryStart())
            {
                _requestsMultiHandler.Start();
                _inputBus.Publish(
                    TimerMessage.Schedule.Create(
                        UpdateInterval, _publishEnvelope, new HttpMessage.PurgeTimedOutRequests(_accessibility)));
            }
            else
            {
                Application.Exit(
                    ExitCode.Error,
                    string.Format(
                        "Http async server failed to start listening at [{0}].",
                        string.Join(", ", _server.ListenPrefixes)));
            }
        }

        public void Handle(SystemMessage.BecomeShuttingDown message)
        {
            if (message.ExitProcess)
                Shutdown();
            _inputBus.Publish(
                new SystemMessage.ServiceShutdown(
                    string.Format("HttpServer [{0}]", string.Join(", ", _server.ListenPrefixes))));
        }

        public void Handle(HttpMessage.SendOverHttp message)
        {
            _httpPipe.Push(message.Message, message.EndPoint);
        }

        private void RequestReceived(HttpAsyncServer sender, HttpListenerContext context)
        {
            var entity = new HttpEntity(context.Request, context.Response, context.User);
            _requestsMultiHandler.Handle(new IncomingHttpRequestMessage(entity, _requestsMultiHandler));
        }

        public void Handle(HttpMessage.PurgeTimedOutRequests message)
        {
            if (_accessibility != message.Accessibility)
                return;

            _requestsMultiHandler.PublishToAll(message);

            _inputBus.Publish(
                TimerMessage.Schedule.Create(
                    UpdateInterval, _publishEnvelope, new HttpMessage.PurgeTimedOutRequests(_accessibility)));
        }

        public void Shutdown()
        {
            _server.Shutdown();
            _requestsMultiHandler.Stop();
        }

        public void SetupController(IController controller)
        {
            Ensure.NotNull(controller, "controller");
            controller.Subscribe(this, _httpPipe);
        }

        public void RegisterControllerAction(
            ControllerAction action, Action<HttpEntityManager, UriTemplateMatch> handler)
        {
            Ensure.NotNull(action, "action");
            Ensure.NotNull(handler, "handler");

            _uriRouter.RegisterControllerAction(action, handler);
        }

        public List<UriToActionMatch> GetAllUriMatches(Uri uri)
        {
            return _uriRouter.GetAllUriMatches(uri);
        }
    }
}
