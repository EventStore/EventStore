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
using EventStore.Projections.Core.Messaging;

namespace EventStore.Projections.Core.Services.Processing
{
    public class RequestResponseQueueForwarder : IHandle<ClientMessage.ReadEvent>,
                                                 IHandle<ClientMessage.ReadEventsBackwards>,
                                                 IHandle<ClientMessage.ReadEventsForward>,
                                                 IHandle<ClientMessage.ReadEventsFromTF>,
                                                 IHandle<ClientMessage.WriteEvents>
    {
        private readonly IPublisher _externalRequestQueue;
        private readonly IPublisher _inputQueue;

        public RequestResponseQueueForwarder(IPublisher inputQueue, IPublisher externalRequestQueue)
        {
            _inputQueue = inputQueue;
            _externalRequestQueue = externalRequestQueue;
        }

        public void Handle(ClientMessage.ReadEvent message)
        {
            _externalRequestQueue.Publish(
                new ClientMessage.ReadEvent(
                    message.CorrelationId, new PublishToWrapEnvelop(_inputQueue, message.Envelope),
                    message.EventStreamId, message.EventNumber, message.ResolveLinkTos));
        }

        public void Handle(ClientMessage.WriteEvents message)
        {
            _externalRequestQueue.Publish(
                new ClientMessage.WriteEvents(
                    message.CorrelationId, new PublishToWrapEnvelop(_inputQueue, message.Envelope),
                    message.EventStreamId, message.ExpectedVersion, message.Events));
        }

        public void Handle(ClientMessage.ReadEventsBackwards message)
        {
            _externalRequestQueue.Publish(
                new ClientMessage.ReadEventsBackwards(
                    message.CorrelationId, new PublishToWrapEnvelop(_inputQueue, message.Envelope),
                    message.EventStreamId, message.FromEventNumber, message.MaxCount, message.ResolveLinks));
        }

        public void Handle(ClientMessage.ReadEventsForward message)
        {
            _externalRequestQueue.Publish(
                new ClientMessage.ReadEventsForward(
                    message.CorrelationId, new PublishToWrapEnvelop(_inputQueue, message.Envelope),
                    message.EventStreamId, message.FromEventNumber, message.MaxCount, message.ResolveLinks));
        }

        public void Handle(ClientMessage.ReadEventsFromTF message)
        {
            _externalRequestQueue.Publish(
                new ClientMessage.ReadEventsFromTF(
                    message.CorrelationId, new PublishToWrapEnvelop(_inputQueue, message.Envelope),
                    message.FromCommitPosition, message.AfterPreparePosition, message.MaxCount, message.ResolveLinks));
        }
    }
}
