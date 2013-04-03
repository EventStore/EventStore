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
using System.Diagnostics;
using System.Net;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
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

        public bool IsListening { get { return _server.IsListening; } }
        public IEnumerable<string> ListenPrefixes { get { return _server.ListenPrefixes; } }

        private readonly ServiceAccessibility _accessibility;
        private readonly IPublisher _inputBus;
        private readonly IEnvelope _publishEnvelope;

        private readonly List<HttpRoute> _actions;

        private readonly HttpMessagePipe _httpPipe;
        private readonly HttpAsyncServer _server;
        private readonly MultiQueuedHandler _requestsMultiHandler;
        private readonly IODispatcher _ioDispatcher;
        private readonly AuthenticationProvider[] _providers;

        public HttpService(
            ServiceAccessibility accessibility, IPublisher inputBus, int receiveHandlerCount, params string[] prefixes)
        {
            Ensure.NotNull(inputBus, "inputBus");
            Ensure.NotNull(prefixes, "prefixes");
            Ensure.Positive(receiveHandlerCount, "receiveHandlerCount");

            _accessibility = accessibility;
            _inputBus = inputBus;
            _publishEnvelope = new PublishEnvelope(inputBus);

            _actions = new List<HttpRoute>();
            _httpPipe = new HttpMessagePipe();
            var queues = new IQueuedHandler[receiveHandlerCount];

            // we need to create multi-handler before queues to resolve dependency into ioDispatcher
            _requestsMultiHandler = new MultiQueuedHandler(queues, null);

            _ioDispatcher = new IODispatcher(inputBus, new PublishEnvelope(_requestsMultiHandler, crossThread: true));
            _providers = new AuthenticationProvider[]
                {
                    new BasicHttpAuthenticationProvider(_ioDispatcher),
                    new AnonymousAuthenticationProvider()
                };

            for (var i = 0; i < receiveHandlerCount; i++)
            {
                queues[i] = CreateQueuedHandler(i);
            }

            _server = new HttpAsyncServer(prefixes);
            _server.RequestReceived += RequestReceived;
        }

        private IQueuedHandler CreateQueuedHandler(int queueNum)
        {
            var bus = new InMemoryBus(string.Format("Incoming HTTP #{0} Bus", queueNum + 1), watchSlowMsg: false);
            var requestProcessor = new AuthenticatedHttpRequestProcessor(this);
            var requestAuthenticationManager = new IncomingHttpRequestAuthenticationManager(_providers);
            bus.Subscribe<IncomingHttpRequestMessage>(requestAuthenticationManager);
            bus.Subscribe<AuthenticatedHttpRequestMessage>(requestProcessor);
            bus.Subscribe<HttpMessage.PurgeTimedOutRequests>(requestProcessor);

            bus.Subscribe(_ioDispatcher.ForwardReader);
            bus.Subscribe(_ioDispatcher.BackwardReader);
            bus.Subscribe(_ioDispatcher.Writer);

            return new QueuedHandlerThreadPool(
                bus, name: "Incoming HTTP #" + (queueNum + 1), groupName: "Incoming HTTP", watchSlowMsg: true,
                slowMsgThreshold: TimeSpan.FromMilliseconds(50));
        }

        public void Handle(SystemMessage.SystemInit message)
        {
            if (_server.TryStart())
            {
                _requestsMultiHandler.Start();
                _inputBus.Publish(TimerMessage.Schedule.Create(UpdateInterval,
                                                               _publishEnvelope,
                                                               new HttpMessage.PurgeTimedOutRequests(_accessibility)));
            }
            else
            {
                Application.Exit(ExitCode.Error,
                                 string.Format("Http async server failed to start listening at [{0}].",
                                               string.Join(", ", _server.ListenPrefixes)));
            }
        }

        public void Handle(SystemMessage.BecomeShuttingDown message)
        {
        }

        public void Handle(HttpMessage.SendOverHttp message)
        {
            _httpPipe.Push(message.Message, message.EndPoint);
        }

        private void RequestReceived(HttpAsyncServer sender, HttpListenerContext context)
        {
            _requestsMultiHandler.Handle(new IncomingHttpRequestMessage(context, _requestsMultiHandler));
        }

        public void Handle(HttpMessage.PurgeTimedOutRequests message)
        {
            if (_accessibility != message.Accessibility)
                return;

            _requestsMultiHandler.PublishToAll(message);

            _inputBus.Publish(TimerMessage.Schedule.Create(UpdateInterval,
                                                           _publishEnvelope,
                                                           new HttpMessage.PurgeTimedOutRequests(_accessibility)));
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

        public void RegisterControllerAction(ControllerAction action, Action<HttpEntityManager, UriTemplateMatch> handler)
        {
            Ensure.NotNull(action, "action");
            Ensure.NotNull(handler, "handler");

            Debug.Assert(!_actions.Contains(x => x.Action.Equals(action)), "Duplicate controller actions.");
            _actions.Add(new HttpRoute(action, handler));
        }

        public List<UriToActionMatch> GetAllUriMatches(Uri uri)
        {
            var matches = new List<UriToActionMatch>();
            var baseAddress = new UriBuilder(uri.Scheme, uri.Host, uri.Port).Uri;
            for (int i = 0; i < _actions.Count; ++i)
            {
                var route = _actions[i];
                var match = route.UriTemplate.Match(baseAddress, uri);
                if (match != null)
                    matches.Add(new UriToActionMatch(match, route.Action, route.Handler));
            }
            return matches;
        }

        private class HttpRoute
        {
            public readonly ControllerAction Action;
            public readonly Action<HttpEntityManager, UriTemplateMatch> Handler;
            public readonly UriTemplate UriTemplate;

            public HttpRoute(ControllerAction action, Action<HttpEntityManager, UriTemplateMatch> handler)
            {
                Action = action;
                Handler = handler;
                UriTemplate = new UriTemplate(action.UriTemplate);
            }
        }
    }
}