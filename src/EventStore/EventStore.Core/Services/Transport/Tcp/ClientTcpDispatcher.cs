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
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.Transport.Tcp
{
    public class ClientTcpDispatcher : TcpDispatcher
    {
        public ClientTcpDispatcher()
        {
            AddUnwrapper(TcpCommand.Ping, UnwrapPing);
            AddWrapper<TcpMessage.PongMessage>(WrapPong);

            AddUnwrapper(TcpCommand.CreateStream, UnwrapCreateStream);
            AddWrapper<ClientMessage.CreateStream>(WrapCreateStream);
            AddUnwrapper(TcpCommand.CreateStreamCompleted, UnwrapCreateStreamCompleted);
            AddWrapper<ClientMessage.CreateStreamCompleted>(WrapCreateStreamCompleted);

            AddUnwrapper(TcpCommand.WriteEvents, UnwrapWriteEvents);
            AddWrapper<ClientMessage.WriteEvents>(WrapWriteEvents);
            AddUnwrapper(TcpCommand.WriteEventsCompleted, UnwrapWriteEventCompleted);
            AddWrapper<ClientMessage.WriteEventsCompleted>(WrapWriteEventsCompleted);

            AddUnwrapper(TcpCommand.TransactionStart, UnwrapTransactionStart);
            AddWrapper<ClientMessage.TransactionStart>(WrapTransactionStart);
            AddUnwrapper(TcpCommand.TransactionStartCompleted, UnwrapTransactionStartCompleted);
            AddWrapper<ClientMessage.TransactionStartCompleted>(WrapTransactionStartCompleted);

            AddUnwrapper(TcpCommand.TransactionWrite, UnwrapTransactionWrite);
            AddWrapper<ClientMessage.TransactionWrite>(WrapTransactionWrite);
            AddUnwrapper(TcpCommand.TransactionWriteCompleted, UnwrapTransactionWriteCompleted);
            AddWrapper<ClientMessage.TransactionWriteCompleted>(WrapTransactionWriteCompleted);

            AddUnwrapper(TcpCommand.TransactionCommit, UnwrapTransactionCommit);
            AddWrapper<ClientMessage.TransactionCommit>(WrapTransactionCommit);
            AddUnwrapper(TcpCommand.TransactionCommitCompleted, UnwrapTransactionCommitCompleted);
            AddWrapper<ClientMessage.TransactionCommitCompleted>(WrapTransactionCommitCompleted);

            AddUnwrapper(TcpCommand.DeleteStream, UnwrapDeleteStream);
            AddWrapper<ClientMessage.DeleteStream>(WrapDeleteStream);
            AddUnwrapper(TcpCommand.DeleteStreamCompleted, UnwrapDeleteStreamCompleted);
            AddWrapper<ClientMessage.DeleteStreamCompleted>(WrapDeleteStreamCompleted);

            AddUnwrapper(TcpCommand.ReadEvent, UnwrapReadEvents);
            AddWrapper<ClientMessage.ReadEventCompleted>(WrapReadEventsCompleted);

            AddUnwrapper(TcpCommand.ReadStreamEventsForward, UnwrapReadStreamEventsForward);
            AddWrapper<ClientMessage.ReadStreamEventsForwardCompleted>(WrapReadStreamEventsForwardCompleted);
            AddUnwrapper(TcpCommand.ReadStreamEventsBackward, UnwrapReadStreamEventsBackward);
            AddWrapper<ClientMessage.ReadStreamEventsBackwardCompleted>(WrapReadStreamEventsBackwardCompleted);

            AddUnwrapper(TcpCommand.ReadAllEventsForward, UnwrapReadAllEventsForward);
            AddWrapper<ClientMessage.ReadAllEventsForwardCompleted>(WrapReadAllEventsForwardCompleted);
            AddUnwrapper(TcpCommand.ReadAllEventsBackward, UnwrapReadAllEventsBackward);
            AddWrapper<ClientMessage.ReadAllEventsBackwardCompleted>(WrapReadAllEventsBackwardCompleted);

            AddUnwrapper(TcpCommand.SubscribeToStream, UnwrapSubscribeToStream);
            AddUnwrapper(TcpCommand.UnsubscribeFromStream, UnwrapUnsubscribeFromStream);

            AddWrapper<ClientMessage.SubscriptionConfirmation>(WrapSubscribedToStream);
            AddWrapper<ClientMessage.StreamEventAppeared>(WrapStreamEventAppeared);
            AddWrapper<ClientMessage.SubscriptionDropped>(WrapSubscriptionDropped);
            
            AddWrapper<ClientMessage.DeniedToRoute>(WrapDeniedToRoute);

            AddUnwrapper(TcpCommand.ScavengeDatabase, UnwrapScavengeDatabase);
        }

        private static Message UnwrapPing(TcpPackage package, IEnvelope envelope)
        {
            var data = new byte[package.Data.Count];
            Buffer.BlockCopy(package.Data.Array, package.Data.Offset, data, 0, package.Data.Count);
            envelope.ReplyWith(new TcpMessage.PongMessage(package.CorrelationId, data));
            return null;
        }

        private static TcpPackage WrapPong(TcpMessage.PongMessage message)
        {
            return new TcpPackage(TcpCommand.Pong, message.CorrelationId, message.Payload);
        }

        private static ClientMessage.CreateStream UnwrapCreateStream(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.CreateStream>();
            if (dto == null) return null;
            return new ClientMessage.CreateStream(package.CorrelationId, envelope, dto.AllowForwarding, dto.EventStreamId, new Guid(dto.RequestId), dto.IsJson, dto.Metadata);
        }

        private static TcpPackage WrapCreateStream(ClientMessage.CreateStream msg)
        {
            var dto = new TcpClientMessageDto.CreateStream(msg.EventStreamId, msg.RequestId.ToByteArray(), msg.Metadata, msg.AllowForwarding, msg.IsJson);
            return new TcpPackage(TcpCommand.CreateStream, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.CreateStreamCompleted UnwrapCreateStreamCompleted(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.CreateStreamCompleted>();
            if (dto == null) return null;
            return new ClientMessage.CreateStreamCompleted(package.CorrelationId, dto.EventStreamId, (OperationResult)dto.Result, dto.Message);
        }

        private static TcpPackage WrapCreateStreamCompleted(ClientMessage.CreateStreamCompleted msg)
        {
            var dto = new TcpClientMessageDto.CreateStreamCompleted(msg.EventStreamId, (TcpClientMessageDto.OperationResult) msg.Result, msg.Message);
            return new TcpPackage(TcpCommand.CreateStreamCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.WriteEvents UnwrapWriteEvents(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.WriteEvents>();
            if (dto == null) return null;
            return new ClientMessage.WriteEvents(
                    package.CorrelationId,
                    envelope,
                    dto.AllowForwarding,
                    dto.EventStreamId,
                    dto.ExpectedVersion,
                    dto.Events.Select(x => new Event(new Guid(x.EventId), x.EventType, x.IsJson,  x.Data, x.Metadata)).ToArray());
        }

        private static TcpPackage WrapWriteEvents(ClientMessage.WriteEvents msg)
        {
            var dto = new TcpClientMessageDto.WriteEvents(
                msg.EventStreamId,
                msg.ExpectedVersion,
                msg.Events.Select(x => new TcpClientMessageDto.NewEvent(x.EventId.ToByteArray(), x.EventType, x.IsJson, x.Data, x.Metadata)).ToArray(),
                msg.AllowForwarding);
            return new TcpPackage(TcpCommand.WriteEvents, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.WriteEventsCompleted UnwrapWriteEventCompleted(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.WriteEventsCompleted>();
            if (dto == null) return null;
            if (dto.Result == TcpClientMessageDto.OperationResult.Success)
                return new ClientMessage.WriteEventsCompleted(package.CorrelationId, dto.EventStreamId, dto.FirstEventNumber);

            return new ClientMessage.WriteEventsCompleted(package.CorrelationId, dto.EventStreamId, (OperationResult) dto.Result, dto.Message);
        }

        private static TcpPackage WrapWriteEventsCompleted(ClientMessage.WriteEventsCompleted msg)
        {
            var dto = new TcpClientMessageDto.WriteEventsCompleted(msg.EventStreamId,
                                                                   (TcpClientMessageDto.OperationResult) msg.Result,
                                                                   msg.Message,
                                                                   msg.FirstEventNumber);
            return new TcpPackage(TcpCommand.WriteEventsCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.TransactionStart UnwrapTransactionStart(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.TransactionStart>();
            if (dto == null) return null;
            return new ClientMessage.TransactionStart(package.CorrelationId, envelope, dto.AllowForwarding, dto.EventStreamId, dto.ExpectedVersion);
        }

        private static TcpPackage WrapTransactionStart(ClientMessage.TransactionStart msg)
        {
            var dto = new TcpClientMessageDto.TransactionStart(msg.EventStreamId, msg.ExpectedVersion, msg.AllowForwarding);
            return new TcpPackage(TcpCommand.TransactionStart, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.TransactionStartCompleted UnwrapTransactionStartCompleted(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.TransactionStartCompleted>();
            if (dto == null) return null;
            return new ClientMessage.TransactionStartCompleted(package.CorrelationId, dto.TransactionId, dto.EventStreamId, (OperationResult) dto.Result, dto.Message);
        }

        private static TcpPackage WrapTransactionStartCompleted(ClientMessage.TransactionStartCompleted msg)
        {
            var dto = new TcpClientMessageDto.TransactionStartCompleted(msg.TransactionId, msg.EventStreamId, (TcpClientMessageDto.OperationResult) msg.Result, msg.Message);
            return new TcpPackage(TcpCommand.TransactionStartCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.TransactionWrite UnwrapTransactionWrite(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.TransactionWrite>();
            if (dto == null) return null;
            return new ClientMessage.TransactionWrite(
                package.CorrelationId,
                envelope,
                dto.AllowForwarding, 
                dto.TransactionId,
                dto.EventStreamId,
                dto.Events.Select(x => new Event(new Guid(x.EventId), x.EventType, x.IsJson,  x.Data, x.Metadata)).ToArray());
        }

        private static TcpPackage WrapTransactionWrite(ClientMessage.TransactionWrite msg)
        {
            var dto = new TcpClientMessageDto.TransactionWrite(msg.TransactionId,
                    msg.EventStreamId,
                    msg.Events.Select(x => new TcpClientMessageDto.NewEvent(x.EventId.ToByteArray(), x.EventType, x.IsJson, x.Data, x.Metadata)).ToArray(),
                    msg.AllowForwarding);
            return new TcpPackage(TcpCommand.TransactionWrite, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.TransactionWriteCompleted UnwrapTransactionWriteCompleted(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.TransactionWriteCompleted>();
            if (dto == null) return null;
            return new ClientMessage.TransactionWriteCompleted(package.CorrelationId, dto.TransactionId, dto.EventStreamId, (OperationResult)dto.Result, dto.Message);
        }

        private static TcpPackage WrapTransactionWriteCompleted(ClientMessage.TransactionWriteCompleted msg)
        {
            var dto = new TcpClientMessageDto.TransactionWriteCompleted(msg.TransactionId, msg.EventStreamId, (TcpClientMessageDto.OperationResult) msg.Result, msg.Message);
            return new TcpPackage(TcpCommand.TransactionWriteCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.TransactionCommit UnwrapTransactionCommit(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.TransactionCommit>();
            if (dto == null) return null;
            return new ClientMessage.TransactionCommit(package.CorrelationId, envelope, dto.AllowForwarding, dto.TransactionId, dto.EventStreamId);
        }

        private static TcpPackage WrapTransactionCommit(ClientMessage.TransactionCommit msg)
        {
            var dto = new TcpClientMessageDto.TransactionCommit(msg.TransactionId, msg.EventStreamId, msg.AllowForwarding);
            return new TcpPackage(TcpCommand.TransactionCommit, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.TransactionCommitCompleted UnwrapTransactionCommitCompleted(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.TransactionCommitCompleted>();
            if (dto == null) return null;
            return new ClientMessage.TransactionCommitCompleted(package.CorrelationId, dto.TransactionId, (OperationResult)dto.Result, dto.Message);
        }

        private static TcpPackage WrapTransactionCommitCompleted(ClientMessage.TransactionCommitCompleted msg)
        {
            var dto = new TcpClientMessageDto.TransactionCommitCompleted(msg.TransactionId, (TcpClientMessageDto.OperationResult) msg.Result, msg.Message);
            return new TcpPackage(TcpCommand.TransactionCommitCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.DeleteStream UnwrapDeleteStream(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.DeleteStream>();
            if (dto == null) return null;
            return new ClientMessage.DeleteStream(package.CorrelationId, envelope, dto.AllowForwarding, dto.EventStreamId, dto.ExpectedVersion);
        }

        private static TcpPackage WrapDeleteStream(ClientMessage.DeleteStream msg)
        {
            var dto = new TcpClientMessageDto.DeleteStream(msg.EventStreamId, msg.ExpectedVersion, msg.AllowForwarding);
            return new TcpPackage(TcpCommand.DeleteStream, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.DeleteStreamCompleted UnwrapDeleteStreamCompleted(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.DeleteStreamCompleted>();
            if (dto == null) return null;
            return new ClientMessage.DeleteStreamCompleted(package.CorrelationId, dto.EventStreamId, (OperationResult) dto.Result, dto.Message); 
        }

        private static TcpPackage WrapDeleteStreamCompleted(ClientMessage.DeleteStreamCompleted msg)
        {
            var dto = new TcpClientMessageDto.DeleteStreamCompleted(msg.EventStreamId, (TcpClientMessageDto.OperationResult) msg.Result, msg.Message);
            return new TcpPackage(TcpCommand.DeleteStreamCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.ReadEvent UnwrapReadEvents(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.ReadEvent>();
            if (dto == null) return null;
            return new ClientMessage.ReadEvent(package.CorrelationId, envelope, dto.EventStreamId, dto.EventNumber, dto.ResolveLinkTos);
        }

        private static TcpPackage WrapReadEventsCompleted(ClientMessage.ReadEventCompleted msg)
        {
            var dto = new TcpClientMessageDto.ReadEventCompleted(msg.EventStreamId,
                                                                 (TcpClientMessageDto.ReadEventCompleted.ReadEventResult) msg.Result,
                                                                 new TcpClientMessageDto.ResolvedIndexedEvent(msg.Record.Event, msg.Record.Link));
            return new TcpPackage(TcpCommand.ReadEventCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.ReadStreamEventsForward UnwrapReadStreamEventsForward(TcpPackage package,
                                                                                           IEnvelope envelope,
                                                                                           TcpConnectionManager connection)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.ReadStreamEvents>();
            if (dto == null) return null;
            return new ClientMessage.ReadStreamEventsForward(package.CorrelationId,
                                                             envelope,
                                                             dto.EventStreamId,
                                                             dto.FromEventNumber,
                                                             dto.MaxCount,
                                                             dto.ResolveLinkTos,
                                                             null);
        }

        private static TcpPackage WrapReadStreamEventsForwardCompleted(ClientMessage.ReadStreamEventsForwardCompleted msg)
        {
            var dto = new TcpClientMessageDto.ReadStreamEventsCompleted(msg.EventStreamId,
                                                                        ConvertToResolvedIndexedEvents(msg.Events),
                                                                        (TcpClientMessageDto.ReadStreamEventsCompleted.ReadStreamResult) msg.Result,
                                                                        msg.NextEventNumber,
                                                                        msg.LastEventNumber,
                                                                        msg.IsEndOfStream,
                                                                        msg.LastCommitPosition);
            return new TcpPackage(TcpCommand.ReadStreamEventsForwardCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.ReadStreamEventsBackward UnwrapReadStreamEventsBackward(TcpPackage package,
                                                                                             IEnvelope envelope,
                                                                                             TcpConnectionManager connection)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.ReadStreamEvents>();
            if (dto == null) return null;
            return new ClientMessage.ReadStreamEventsBackward(package.CorrelationId, 
                                                              envelope,
                                                              dto.EventStreamId,
                                                              dto.FromEventNumber,
                                                              dto.MaxCount,
                                                              dto.ResolveLinkTos,
                                                              null);
        }

        private static TcpPackage WrapReadStreamEventsBackwardCompleted(ClientMessage.ReadStreamEventsBackwardCompleted msg)
        {
            var dto = new TcpClientMessageDto.ReadStreamEventsCompleted(msg.EventStreamId,
                                                                        ConvertToResolvedIndexedEvents(msg.Events),
                                                                        (TcpClientMessageDto.ReadStreamEventsCompleted.ReadStreamResult) msg.Result,
                                                                        msg.NextEventNumber,
                                                                        msg.LastEventNumber,
                                                                        msg.IsEndOfStream,
                                                                        msg.LastCommitPosition);
            return new TcpPackage(TcpCommand.ReadStreamEventsBackwardCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static TcpClientMessageDto.ResolvedIndexedEvent[] ConvertToResolvedIndexedEvents(ResolvedEvent[] events)
        {
            var result = new TcpClientMessageDto.ResolvedIndexedEvent[events.Length];
            for (int i = 0; i < events.Length; ++i)
            {
                result[i] = new TcpClientMessageDto.ResolvedIndexedEvent(events[i].Event, events[i].Link);
            }
            return result;
        }

        private static ClientMessage.ReadAllEventsForward UnwrapReadAllEventsForward(TcpPackage package, IEnvelope envelope, TcpConnectionManager connection)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.ReadAllEvents>();
            if (dto == null) return null;
            return new ClientMessage.ReadAllEventsForward(package.CorrelationId,
                                                          envelope,
                                                          dto.CommitPosition,
                                                          dto.PreparePosition,
                                                          dto.MaxCount,
                                                          dto.ResolveLinkTos,
                                                          validationTfEofPosition: null);
        }

        private static TcpPackage WrapReadAllEventsForwardCompleted(ClientMessage.ReadAllEventsForwardCompleted msg)
        {
            var dto = new TcpClientMessageDto.ReadAllEventsCompleted(msg.Result.CurrentPos.CommitPosition,
                                                                     msg.Result.CurrentPos.PreparePosition,
                                                                     ConvertToResolvedEvents(msg.Result.Events),
                                                                     msg.Result.NextPos.CommitPosition,
                                                                     msg.Result.NextPos.PreparePosition);
            return new TcpPackage(TcpCommand.ReadAllEventsForwardCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.ReadAllEventsBackward UnwrapReadAllEventsBackward(TcpPackage package, IEnvelope envelope, TcpConnectionManager connection)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.ReadAllEvents>();
            if (dto == null) return null;
            return new ClientMessage.ReadAllEventsBackward(package.CorrelationId,
                                                           envelope,
                                                           dto.CommitPosition,
                                                           dto.PreparePosition,
                                                           dto.MaxCount,
                                                           dto.ResolveLinkTos,
                                                           validationTfEofPosition: null);
        }

        private static TcpPackage WrapReadAllEventsBackwardCompleted(ClientMessage.ReadAllEventsBackwardCompleted msg)
        {
            var dto = new TcpClientMessageDto.ReadAllEventsCompleted(msg.Result.CurrentPos.CommitPosition,
                                                                     msg.Result.CurrentPos.PreparePosition,
                                                                     ConvertToResolvedEvents(msg.Result.Events),
                                                                     msg.Result.NextPos.CommitPosition,
                                                                     msg.Result.NextPos.PreparePosition);
            return new TcpPackage(TcpCommand.ReadAllEventsBackwardCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static TcpClientMessageDto.ResolvedEvent[] ConvertToResolvedEvents(ResolvedEvent[] events)
        {
            var result = new TcpClientMessageDto.ResolvedEvent[events.Length];
            for (int i = 0; i < events.Length; ++i)
            {
                result[i] = new TcpClientMessageDto.ResolvedEvent(events[i]);
            }
            return result;
        }

        private ClientMessage.SubscribeToStream UnwrapSubscribeToStream(TcpPackage package, IEnvelope envelope, TcpConnectionManager connection)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.SubscribeToStream>();
            if (dto == null) return null;
            return new ClientMessage.SubscribeToStream(connection, package.CorrelationId, dto.EventStreamId, dto.ResolveLinkTos);
        }

        private ClientMessage.UnsubscribeFromStream UnwrapUnsubscribeFromStream(TcpPackage package, IEnvelope envelope, TcpConnectionManager connection)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.UnsubscribeFromStream>();
            if (dto == null) return null;
            return new ClientMessage.UnsubscribeFromStream(package.CorrelationId);
        }

        private TcpPackage WrapSubscribedToStream(ClientMessage.SubscriptionConfirmation msg)
        {
            var dto = new TcpClientMessageDto.SubscriptionConfirmation(msg.LastCommitPosition, msg.LastEventNumber);
            return new TcpPackage(TcpCommand.SubscriptionConfirmation, msg.CorrelationId, dto.Serialize());
        }

        private TcpPackage WrapStreamEventAppeared(ClientMessage.StreamEventAppeared msg)
        {
            var dto = new TcpClientMessageDto.StreamEventAppeared(new TcpClientMessageDto.ResolvedEvent(msg.Event));
            return new TcpPackage(TcpCommand.StreamEventAppeared, msg.CorrelationId, dto.Serialize());
        }

        private TcpPackage WrapSubscriptionDropped(ClientMessage.SubscriptionDropped msg)
        {
            var dto = new TcpClientMessageDto.SubscriptionDropped(msg.EventStreamId);
            return new TcpPackage(TcpCommand.SubscriptionDropped, msg.CorrelationId, dto.Serialize());
        }

        private TcpPackage WrapDeniedToRoute(ClientMessage.DeniedToRoute msg)
        {
            var dto = new TcpClientMessageDto.DeniedToRoute(msg.ExternalTcpEndPoint,
                                                         msg.ExternalHttpEndPoint);
            return new TcpPackage(TcpCommand.DeniedToRoute, msg.CorrelationId, dto.Serialize());
        }

        private SystemMessage.ScavengeDatabase UnwrapScavengeDatabase(TcpPackage package, IEnvelope envelope)
        {
            return new SystemMessage.ScavengeDatabase();
        }
    }
}