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
using System.Security.Principal;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services;

namespace EventStore.Core.Helpers
{
    public sealed class IODispatcher
    {
        public readonly
            RequestResponseDispatcher
                <ClientMessage.ReadStreamEventsForward, ClientMessage.ReadStreamEventsForwardCompleted> ForwardReader;

        public readonly
            RequestResponseDispatcher
                <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted> BackwardReader;

        public readonly RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted> Writer;

        public readonly RequestResponseDispatcher<ClientMessage.DeleteStream, ClientMessage.DeleteStreamCompleted> StreamDeleter;

        public IODispatcher(IPublisher publisher, IEnvelope envelope)
        {
            ForwardReader =
                new RequestResponseDispatcher
                    <ClientMessage.ReadStreamEventsForward, ClientMessage.ReadStreamEventsForwardCompleted>(
                    publisher, v => v.CorrelationId, v => v.CorrelationId, envelope);
            BackwardReader =
                new RequestResponseDispatcher
                    <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted>(
                    publisher, v => v.CorrelationId, v => v.CorrelationId, envelope);
            Writer =
                new RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted>(
                    publisher, v => v.CorrelationId, v => v.CorrelationId, envelope);

            StreamDeleter =
                new RequestResponseDispatcher<ClientMessage.DeleteStream, ClientMessage.DeleteStreamCompleted>(
                    publisher, v => v.CorrelationId, v => v.CorrelationId, envelope);
        }

        public void ReadBackward(
            string streamId, int fromEventNumber, int maxCount, bool resolveLinks, IPrincipal principal,
            Action<ClientMessage.ReadStreamEventsBackwardCompleted> action)
        {
            BackwardReader.Publish(
                new ClientMessage.ReadStreamEventsBackward(
                    Guid.NewGuid(), BackwardReader.Envelope, streamId, fromEventNumber, maxCount, resolveLinks, null, principal),
                action);
        }

        public void ReadForward(
            string streamId, int fromEventNumber, int maxCount, bool resolveLinks, IPrincipal principal,
            Action<ClientMessage.ReadStreamEventsForwardCompleted> action)
        {
            ForwardReader.Publish(
                new ClientMessage.ReadStreamEventsForward(
                    Guid.NewGuid(), ForwardReader.Envelope, streamId, fromEventNumber, maxCount, resolveLinks, null, principal),
                action);
        }

        public void ConfigureStreamAndWriteEvents(
            string streamId, int expectedVersion, Lazy<StreamMetadata> streamMetadata, Event[] events,
            IPrincipal principal, Action<ClientMessage.WriteEventsCompleted> action)
        {
            if (expectedVersion != ExpectedVersion.Any && expectedVersion != ExpectedVersion.NoStream)
                WriteEvents(streamId, expectedVersion, events, principal, action);
            else
                ReadBackward(
                    streamId, -1, 1, false, principal, completed =>
                        {
                            switch (completed.Result)
                            {
                                case ReadStreamResult.Success:
                                case ReadStreamResult.NoStream:
                                    if (completed.Events != null && completed.Events.Length > 0)
                                        WriteEvents(streamId, expectedVersion, events, principal, action);
                                    else
                                        UpdateStreamAcl(
                                            streamId, ExpectedVersion.Any, principal, streamMetadata.Value,
                                            metaCompleted =>
                                            WriteEvents(streamId, expectedVersion, events, principal, action));
                                    break;
                                case ReadStreamResult.AccessDenied:
                                    action(
                                        new ClientMessage.WriteEventsCompleted(
                                            Guid.NewGuid(), OperationResult.AccessDenied, ""));
                                    break;
                                case ReadStreamResult.StreamDeleted:
                                    action(
                                        new ClientMessage.WriteEventsCompleted(
                                            Guid.NewGuid(), OperationResult.StreamDeleted, ""));
                                    break;
                                default:
                                    throw new NotSupportedException();
                            }
                        });
        }

        public void WriteEvents(
            string streamId, int expectedVersion, Event[] events, IPrincipal principal, 
            Action<ClientMessage.WriteEventsCompleted> action)
        {
            Writer.Publish(
                new ClientMessage.WriteEvents(Guid.NewGuid(), Writer.Envelope, true, streamId, expectedVersion, events, principal),
                action);
        }

        public void DeleteStream(
            string streamId, int expectedVersion, IPrincipal principal, 
            Action<ClientMessage.DeleteStreamCompleted> action)
        {
            StreamDeleter.Publish(
                new ClientMessage.DeleteStream(Guid.NewGuid(), Writer.Envelope, true, streamId, expectedVersion, principal), action);
        }

        public void UpdateStreamAcl(
            string streamId, int expectedVersion, IPrincipal principal, StreamMetadata metadata,
            Action<ClientMessage.WriteEventsCompleted> completed)
        {
            WriteEvents(
                SystemStreams.MetastreamOf(streamId), expectedVersion,
                new[] {new Event(Guid.NewGuid(), SystemEventTypes.StreamMetadata, true, metadata.ToJsonBytes(), null)},
                principal, completed);
        }
    }
}
