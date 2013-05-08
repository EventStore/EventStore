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
using EventStore.Core;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.Services.Transport.Http.Authentication;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Core.Services.Transport.Http.Messages;
using EventStore.Core.Services.Transport.Tcp;

namespace EventStore.Web.Playground
{
    public class PlaygroundVNode
    {
        private QueuedHandler MainQueue
        {
            get { return _mainQueue; }
        }

        private InMemoryBus Bus
        {
            get { return _mainBus; }
        }

        private HttpService HttpService
        {
            get { return _httpService; }
        }

        private TimerService TimerService
        {
            get { return _timerService; }
        }

        private readonly IPEndPoint _tcpEndPoint;
        private readonly IPEndPoint _httpEndPoint;

        private readonly QueuedHandler _mainQueue;
        private readonly InMemoryBus _mainBus;

        private readonly PlaygroundVNodeController _controller;
        private readonly HttpService _httpService;
        private readonly TimerService _timerService;
        private readonly NetworkSendService _networkSendService;

        private readonly NodeSubsystems[] _enabledNodeSubsystems;

        public PlaygroundVNode(PlaygroundVNodeSettings vNodeSettings)
        {
            _tcpEndPoint = vNodeSettings.ExternalTcpEndPoint;
            _httpEndPoint = vNodeSettings.ExternalHttpEndPoint;

            _mainBus = new InMemoryBus("MainBus");
            _controller = new PlaygroundVNodeController(Bus, _httpEndPoint);
            _mainQueue = new QueuedHandler(_controller, "MainQueue");
            _controller.SetMainQueue(MainQueue);

            _enabledNodeSubsystems = new NodeSubsystems[0];

            // MONITORING
            var monitoringInnerBus = new InMemoryBus("MonitoringInnerBus", watchSlowMsg: false);
            var monitoringQueue = new QueuedHandler(
                monitoringInnerBus, "MonitoringQueue", true, TimeSpan.FromMilliseconds(100));
            Bus.Subscribe(monitoringQueue.WidenFrom<SystemMessage.SystemInit, Message>());
            Bus.Subscribe(monitoringQueue.WidenFrom<SystemMessage.StateChangeMessage, Message>());
            Bus.Subscribe(monitoringQueue.WidenFrom<SystemMessage.BecomeShuttingDown, Message>());


            // NETWORK SEND
            _networkSendService = new NetworkSendService(
                tcpQueueCount: vNodeSettings.TcpSendingThreads, httpQueueCount: vNodeSettings.HttpSendingThreads);

            // TCP
            var tcpService = new TcpService(
                MainQueue, _tcpEndPoint, _networkSendService, TcpServiceType.External, TcpSecurityType.Normal, new ClientTcpDispatcher(), null);
            Bus.Subscribe<SystemMessage.SystemInit>(tcpService);
            Bus.Subscribe<SystemMessage.SystemStart>(tcpService);
            Bus.Subscribe<SystemMessage.BecomeShuttingDown>(tcpService);

            // HTTP
            {

                var queues = new IQueuedHandler[vNodeSettings.HttpReceivingThreads];
                var buses = new InMemoryBus[vNodeSettings.HttpReceivingThreads];
                var multiQueuedHandler = new MultiQueuedHandler(queues, null);
                for (var i = 0; i < vNodeSettings.HttpReceivingThreads; i++)
                {
                    buses[i] = new InMemoryBus(string.Format("Incoming HTTP #{0} Bus", i + 1), watchSlowMsg: false);
                    queues[i] = new QueuedHandlerThreadPool(
                        buses[i], name: "Incoming HTTP #" + (i + 1), groupName: "Incoming HTTP", watchSlowMsg: true,
                        slowMsgThreshold: TimeSpan.FromMilliseconds(50));
                }

                var dispatcher = new IODispatcher(
                    _mainQueue, new PublishEnvelope(multiQueuedHandler, crossThread: true));

                var passwordHashAlgorithm = new Rfc2898PasswordHashAlgorithm();
                var internalAuthenticationProvider = new InternalAuthenticationProvider(dispatcher, passwordHashAlgorithm, 1000);
                var authenticationProviders = new AuthenticationProvider[]
                    {
                        new BasicHttpAuthenticationProvider(internalAuthenticationProvider),
                        new TrustedAuthenticationProvider(), new AnonymousAuthenticationProvider()
                    };


                _httpService = new HttpService(
                    ServiceAccessibility.Private, _mainQueue, new TrieUriRouter(), multiQueuedHandler,
                    authenticationProviders, vNodeSettings.HttpPrefixes);


                for (var i = 0; i < vNodeSettings.HttpReceivingThreads; i++)
                {
                    var bus = buses[i];

                    bus.Subscribe(dispatcher.ForwardReader);
                    bus.Subscribe(dispatcher.BackwardReader);
                    bus.Subscribe(dispatcher.Writer);
                    bus.Subscribe(dispatcher.StreamDeleter);

                    _httpService.CreateAndSubscribePipeline(bus);
                }

                _mainBus.Subscribe<SystemMessage.SystemInit>(HttpService);
                _mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(HttpService);
                _mainBus.Subscribe<HttpMessage.SendOverHttp>(HttpService);
                _mainBus.Subscribe<HttpMessage.PurgeTimedOutRequests>(HttpService);
                HttpService.SetupController(new AdminController(_mainQueue));
                HttpService.SetupController(new PingController());
                HttpService.SetupController(new StatController(monitoringQueue, _networkSendService));
                HttpService.SetupController(new AtomController(_mainQueue, _networkSendService));
                HttpService.SetupController(new UsersController(_mainQueue, _networkSendService));
            }


            // TIMER
            _timerService = new TimerService(new ThreadBasedScheduler(new RealTimeProvider()));
            Bus.Subscribe(TimerService);

            monitoringQueue.Start();
            MainQueue.Start();
        }

        public void Start()
        {
            MainQueue.Publish(new SystemMessage.SystemInit());
        }

        public void Stop(bool exitProcess)
        {
            MainQueue.Publish(new ClientMessage.RequestShutdown(exitProcess));
        }

        public override string ToString()
        {
            return string.Format("[{0}, {1}]", _tcpEndPoint, _httpEndPoint);
        }
    }
}
