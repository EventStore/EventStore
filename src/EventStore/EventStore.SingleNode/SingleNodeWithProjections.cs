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
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Projections.Core;
using EventStore.Projections.Core.Messaging;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.SingleNode
{
    public class SingleNodeWithProjections  : SingleNode.Program, IHandle<SystemMessage.SystemStart>
    {
        private InMemoryBus _coreInputBus;
        private QueuedHandler _coreQueue;
        private ProjectionWorkerNode _projectionNode;
        private RequestResponseQueueForwarder _forwarder;
        private InMemoryBus _readerInputBus;

        public static int Main(string[] args)
        {
            var p = new SingleNodeWithProjections();
            return p.Run(args);
        }

        protected override void Create()
        {
            base.Create();
            var db = this.TfDb;
            _coreInputBus = new InMemoryBus("bus");
            _coreQueue = new QueuedHandler(_coreInputBus, "ProjectionCoreQueue");

            _readerInputBus = new InMemoryBus("Reader Input");

            _projectionNode = new ProjectionWorkerNode(db, _coreQueue, Node.HttpService);
            _projectionNode.SetupMessaging(_coreInputBus, _readerInputBus);

            _forwarder = new RequestResponseQueueForwarder(inputQueue: _coreQueue, externalRequestQueue: Node.MainQueue);
            // forwarded messages
            _projectionNode.CoreOutput.Subscribe<ClientMessage.ReadEvent>(_forwarder);
            _projectionNode.CoreOutput.Subscribe<ClientMessage.ReadEventsBackwards>(_forwarder);
            _projectionNode.CoreOutput.Subscribe<ClientMessage.ReadEventsForward>(_forwarder);
            _projectionNode.CoreOutput.Subscribe<ClientMessage.ReadEventsFromTF>(_forwarder);
            _projectionNode.CoreOutput.Subscribe<ClientMessage.WriteEvents>(_forwarder);
            _coreInputBus.Subscribe(new UnwrapEnvelopeHandler());

            Node.Bus.Subscribe(Forwarder.Create<SystemMessage.BecomeShuttingDown>(_coreQueue));
            Node.Bus.Subscribe(Forwarder.Create<SystemMessage.SystemInit>(_coreQueue));
            Node.Bus.Subscribe(Forwarder.Create<SystemMessage.SystemStart>(_coreQueue));

            _coreInputBus.Subscribe<SystemMessage.SystemStart>(this);
        }

        protected override void Start()
        {
            _coreQueue.Start();
            base.Start();
        }

        public void Handle(SystemMessage.SystemStart message)
        {
        }
    }
}
