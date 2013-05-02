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

using System.Collections.Generic;
using System.Linq;
using EventStore.Core;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messaging;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core
{
    public sealed class ProjectionsSubsystem : ISubsystem
    {
        private Projections _projections;
        private readonly int _projectionWorkerThreadCount;
        private readonly bool _runProjections;

        public ProjectionsSubsystem(int projectionWorkerThreadCount, bool runProjections)
        {
            _projectionWorkerThreadCount = projectionWorkerThreadCount;
            _runProjections = runProjections;
        }

        public void Register(TFChunkDb db, QueuedHandler mainQueue, ISubscriber mainBus, TimerService timerService, HttpService httpService, IPublisher networkSendService)
        {
            _projections = new EventStore.Projections.Core.Projections(db, 
                                                            mainQueue,
                                                            mainBus,
                                                            timerService,
                                                            httpService,
                                                            networkSendService,
                                                            projectionWorkerThreadCount: _projectionWorkerThreadCount, runProjections: _runProjections);
        }

        public void Start()
        {
            _projections.Start();
        }

        public void Stop()
        {
            throw new System.NotImplementedException();
        }
    }

    class Projections
    {
        private List<QueuedHandler> _coreQueues;
        private readonly int _projectionWorkerThreadCount;
        private QueuedHandler _managerInputQueue;
        private InMemoryBus _managerInputBus;
        private ProjectionManagerNode _projectionManagerNode;

        public Projections(
            TFChunkDb db, QueuedHandler mainQueue, ISubscriber mainBus, TimerService timerService,
            HttpService httpService, IPublisher networkSendQueue, int projectionWorkerThreadCount, bool runProjections)
        {
            _projectionWorkerThreadCount = projectionWorkerThreadCount;
            SetupMessaging(db, mainQueue, mainBus, timerService, httpService, networkSendQueue, runProjections);

        }

        private void SetupMessaging(
            TFChunkDb db, QueuedHandler mainQueue, ISubscriber mainBus, TimerService timerService,
            HttpService httpService, IPublisher networkSendQueue, bool runProjections)
        {
            _coreQueues = new List<QueuedHandler>();
            if (runProjections)
            {
                _managerInputBus = new InMemoryBus("manager input bus");
                _managerInputQueue = new QueuedHandler(_managerInputBus, "ProjectionManager");
            }
            while (_coreQueues.Count < _projectionWorkerThreadCount)
            {
                var coreInputBus = new InMemoryBus("bus");
                var coreQueue = new QueuedHandler(
                    coreInputBus, "Projection Core #" + _coreQueues.Count, groupName: "Projection Core");
                var projectionNode = new ProjectionWorkerNode(db, coreQueue, runProjections);
                projectionNode.SetupMessaging(coreInputBus);


                var forwarder = new RequestResponseQueueForwarder(
                    inputQueue: coreQueue, externalRequestQueue: mainQueue);
                // forwarded messages
                projectionNode.CoreOutput.Subscribe<ClientMessage.ReadEvent>(forwarder);
                projectionNode.CoreOutput.Subscribe<ClientMessage.ReadStreamEventsBackward>(forwarder);
                projectionNode.CoreOutput.Subscribe<ClientMessage.ReadStreamEventsForward>(forwarder);
                projectionNode.CoreOutput.Subscribe<ClientMessage.ReadAllEventsForward>(forwarder);
                projectionNode.CoreOutput.Subscribe<ClientMessage.WriteEvents>(forwarder);


                if (runProjections)
                {
                    projectionNode.CoreOutput.Subscribe(
                        Forwarder.Create<CoreProjectionManagementMessage.StateReport>(_managerInputQueue));
                    projectionNode.CoreOutput.Subscribe(
                        Forwarder.Create<CoreProjectionManagementMessage.ResultReport>(_managerInputQueue));
                    projectionNode.CoreOutput.Subscribe(
                        Forwarder.Create<CoreProjectionManagementMessage.DebugState>(_managerInputQueue));
                    projectionNode.CoreOutput.Subscribe(
                        Forwarder.Create<CoreProjectionManagementMessage.StatisticsReport>(_managerInputQueue));
                    projectionNode.CoreOutput.Subscribe(
                        Forwarder.Create<CoreProjectionManagementMessage.Started>(_managerInputQueue));
                    projectionNode.CoreOutput.Subscribe(
                        Forwarder.Create<CoreProjectionManagementMessage.Stopped>(_managerInputQueue));
                    projectionNode.CoreOutput.Subscribe(
                        Forwarder.Create<CoreProjectionManagementMessage.Faulted>(_managerInputQueue));
                    projectionNode.CoreOutput.Subscribe(
                        Forwarder.Create<CoreProjectionManagementMessage.Prepared>(_managerInputQueue));

                }
                projectionNode.CoreOutput.Subscribe(timerService);


                projectionNode.CoreOutput.Subscribe(Forwarder.Create<Message>(coreQueue)); // forward all

                coreInputBus.Subscribe(new UnwrapEnvelopeHandler());

                _coreQueues.Add(coreQueue);
            }

            if (runProjections)
            {
                _projectionManagerNode = ProjectionManagerNode.Create(
                    db, _managerInputQueue, httpService, networkSendQueue, _coreQueues.Cast<IPublisher>().ToArray());
                _projectionManagerNode.SetupMessaging(_managerInputBus);
                {
                    var forwarder = new RequestResponseQueueForwarder(
                        inputQueue: _managerInputQueue, externalRequestQueue: mainQueue);
                    _projectionManagerNode.Output.Subscribe<ClientMessage.ReadEvent>(forwarder);
                    _projectionManagerNode.Output.Subscribe<ClientMessage.ReadStreamEventsBackward>(forwarder);
                    _projectionManagerNode.Output.Subscribe<ClientMessage.ReadStreamEventsForward>(forwarder);
                    _projectionManagerNode.Output.Subscribe<ClientMessage.WriteEvents>(forwarder);
                    _projectionManagerNode.Output.Subscribe(
                        Forwarder.Create<ProjectionManagementMessage.RequestSystemProjections>(mainQueue));
                    _projectionManagerNode.Output.Subscribe(Forwarder.Create<Message>(_managerInputQueue));

                    _projectionManagerNode.Output.Subscribe(timerService);

                    // self forward all

                    mainBus.Subscribe(Forwarder.Create<SystemMessage.StateChangeMessage>(_managerInputQueue));
                    _managerInputBus.Subscribe(new UnwrapEnvelopeHandler());
                }
            }
        }

        public void Start()
        {
            _managerInputQueue.Start();
            foreach (var queue in _coreQueues)
                queue.Start();
        }
    }
}
