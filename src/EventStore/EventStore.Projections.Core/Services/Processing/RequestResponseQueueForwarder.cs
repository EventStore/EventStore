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
                                                 IHandle<ClientMessage.ReadStreamEventsBackward>,
                                                 IHandle<ClientMessage.ReadStreamEventsForward>,
                                                 IHandle<ClientMessage.ReadAllEventsForward>,
                                                 IHandle<ClientMessage.WriteEvents>
    {
        private readonly IPublisher _externalRequestQueue;
        private readonly IPublisher _inputQueue;

        public RequestResponseQueueForwarder(IPublisher inputQueue, IPublisher externalRequestQueue)
        {
            _inputQueue = inputQueue;
            _externalRequestQueue = externalRequestQueue;
        }

        public void Handle(ClientMessage.ReadEvent msg)
        {
            _externalRequestQueue.Publish(
                new ClientMessage.ReadEvent(
                    msg.InternalCorrId, msg.CorrelationId, new PublishToWrapEnvelop(_inputQueue, msg.Envelope),
                    msg.EventStreamId, msg.EventNumber, msg.ResolveLinkTos, msg.RequireMaster, msg.User));
        }

        public void Handle(ClientMessage.WriteEvents msg)
        {
            _externalRequestQueue.Publish(
                new ClientMessage.WriteEvents(
                    msg.InternalCorrId, msg.CorrelationId, new PublishToWrapEnvelop(_inputQueue, msg.Envelope), false, 
                    msg.EventStreamId, msg.ExpectedVersion, msg.Events, msg.User));
        }

        public void Handle(ClientMessage.ReadStreamEventsBackward msg)
        {
            _externalRequestQueue.Publish(
                new ClientMessage.ReadStreamEventsBackward(
                    msg.InternalCorrId, msg.CorrelationId, new PublishToWrapEnvelop(_inputQueue, msg.Envelope),
                    msg.EventStreamId, msg.FromEventNumber, msg.MaxCount, msg.ResolveLinkTos, msg.RequireMaster,
                    msg.ValidationStreamVersion, msg.User));
        }

        public void Handle(ClientMessage.ReadStreamEventsForward msg)
        {
            _externalRequestQueue.Publish(
                new ClientMessage.ReadStreamEventsForward(
                    msg.InternalCorrId, msg.CorrelationId, new PublishToWrapEnvelop(_inputQueue, msg.Envelope),
                    msg.EventStreamId, msg.FromEventNumber, msg.MaxCount, msg.ResolveLinkTos, msg.RequireMaster,
                    msg.ValidationStreamVersion, msg.User));
        }

        public void Handle(ClientMessage.ReadAllEventsForward msg)
        {
            _externalRequestQueue.Publish(
                new ClientMessage.ReadAllEventsForward(
                    msg.InternalCorrId, msg.CorrelationId, new PublishToWrapEnvelop(_inputQueue, msg.Envelope),
                    msg.CommitPosition, msg.PreparePosition, msg.MaxCount, msg.ResolveLinkTos, msg.RequireMaster,
                    msg.ValidationTfEofPosition, msg.User));
        }
    }
}
