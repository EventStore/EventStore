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
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Projections.Core;
using EventStore.Projections.Core.Messaging;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.SingleNode
{
    public class Projections  
    {
        private List<QueuedHandler> _coreQueues;
        private readonly int _projectionWorkerThreadCount;

        public Projections(TFChunkDb db, QueuedHandler mainQueue, InMemoryBus mainBus, TimerService timerService, HttpService httpService, int projectionWorkerThreadCount)
        {
            SetupMessaging(db, mainQueue, mainBus, timerService, httpService);
            _projectionWorkerThreadCount = projectionWorkerThreadCount;
        }

        private void SetupMessaging(
            TFChunkDb db, QueuedHandler mainQueue, InMemoryBus mainBus, TimerService timerService,
            HttpService httpService)
        {
            _coreQueues = new List<QueuedHandler>();

            while (_coreQueues.Count < _projectionWorkerThreadCount)
            {
                var coreInputBus = new InMemoryBus("bus");
                var coreQueue = new QueuedHandler(coreInputBus, "ProjectionCoreQueue");
                var projectionNode = new ProjectionWorkerNode(db);
                projectionNode.SetupMessaging(coreInputBus);


                var forwarder = new RequestResponseQueueForwarder(
                    inputQueue: coreQueue, externalRequestQueue: mainQueue);
                // forwarded messages
                projectionNode.CoreOutput.Subscribe<ClientMessage.ReadEvent>(forwarder);
                projectionNode.CoreOutput.Subscribe<ClientMessage.ReadEventsBackwards>(forwarder);
                projectionNode.CoreOutput.Subscribe<ClientMessage.ReadEventsForward>(forwarder);
                projectionNode.CoreOutput.Subscribe<ClientMessage.ReadEventsFromTF>(forwarder);
                projectionNode.CoreOutput.Subscribe<ClientMessage.WriteEvents>(forwarder);

                projectionNode.CoreOutput.Subscribe(timerService);


                projectionNode.CoreOutput.Subscribe(Forwarder.Create<Message>(coreQueue)); // forward all

                coreInputBus.Subscribe(new UnwrapEnvelopeHandler());

                _coreQueues.Add(coreQueue);
            }


            var projectionManagerNode = ProjectionManagerNode.Create(
                db, mainQueue, httpService, _coreQueues.Cast<IPublisher>().ToArray());
            projectionManagerNode.SetupMessaging(mainBus);
        }

        public void Start()
        {
            foreach(var queue in _coreQueues)
                queue.Start();
        }
    }
}
