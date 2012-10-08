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

using EventStore.Core;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Projections.Core;
using EventStore.Projections.Core.Messaging;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.SingleNode
{
    public class Projections  
    {
        // TODO MM, YS: why isnt this code inside  ProjectionWorkerNode.cs ?

        private readonly InMemoryBus _coreInputBus;
        private readonly QueuedHandler _coreQueue;
        private readonly ProjectionWorkerNode _projectionNode;
        private readonly RequestResponseQueueForwarder _forwarder;
        private readonly InMemoryBus _readerInputBus;

        public Projections(SingleVNode node, TFChunkDb db)
        {
            _coreInputBus = new InMemoryBus("bus");
            _coreQueue = new QueuedHandler(_coreInputBus, "ProjectionCoreQueue");

            _readerInputBus = new InMemoryBus("Reader Input");

            _projectionNode = new ProjectionWorkerNode(db, _coreQueue, node.HttpService);
            _projectionNode.SetupMessaging(_coreInputBus, _readerInputBus);

            _forwarder = new RequestResponseQueueForwarder(inputQueue: _coreQueue, externalRequestQueue: node.MainQueue);
            // forwarded messages
            _projectionNode.CoreOutput.Subscribe<ClientMessage.ReadEvent>(_forwarder);
            _projectionNode.CoreOutput.Subscribe<ClientMessage.ReadEventsBackwards>(_forwarder);
            _projectionNode.CoreOutput.Subscribe<ClientMessage.ReadEventsForward>(_forwarder);
            _projectionNode.CoreOutput.Subscribe<ClientMessage.ReadEventsFromTF>(_forwarder);
            _projectionNode.CoreOutput.Subscribe<ClientMessage.WriteEvents>(_forwarder);
            _coreInputBus.Subscribe(new UnwrapEnvelopeHandler());

            node.Bus.Subscribe(Forwarder.Create<SystemMessage.BecomeShuttingDown>(_coreQueue));
            node.Bus.Subscribe(Forwarder.Create<SystemMessage.SystemInit>(_coreQueue));
            node.Bus.Subscribe(Forwarder.Create<SystemMessage.SystemStart>(_coreQueue));

            _projectionNode.CoreOutput.Subscribe(node.TimerService);
        }

        public void Start()
        {
            _coreQueue.Start();
        }
    }
}
