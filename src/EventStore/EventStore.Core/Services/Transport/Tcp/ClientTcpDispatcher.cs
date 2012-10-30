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

            AddUnwrapper(TcpCommand.SubscribeToAllStreams, UnwrapSubscribeToAllStreams);
            AddUnwrapper(TcpCommand.UnsubscribeFromAllStreams, UnwrapUnsubscribeFromAllStreams);

            AddWrapper<ClientMessage.StreamEventAppeared>(WrapStreamEventAppeared);
            AddWrapper<ClientMessage.SubscriptionDropped>(WrapSubscriptionDropped);
            AddWrapper<ClientMessage.SubscriptionToAllDropped>(WrapSubscriptionToAllDropped);
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
            var dto = package.Data.Deserialize<ClientMessageDto.CreateStream>();
            if (dto == null) return null;
            return new ClientMessage.CreateStream(package.CorrelationId, envelope, dto.AllowForwarding, dto.EventStreamId, dto.Metadata);
        }

        private static TcpPackage WrapCreateStream(ClientMessage.CreateStream msg)
        {
            var dto = new ClientMessageDto.CreateStream(msg.EventStreamId, msg.Metadata, msg.AllowForwarding);
            return new TcpPackage(TcpCommand.CreateStream, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.CreateStreamCompleted UnwrapCreateStreamCompleted(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.CreateStreamCompleted>();
            if (dto == null) return null;
            return new ClientMessage.CreateStreamCompleted(package.CorrelationId, dto.EventStreamId, (OperationErrorCode)dto.ErrorCode, dto.Error);
        }

        private static TcpPackage WrapCreateStreamCompleted(ClientMessage.CreateStreamCompleted msg)
        {
            var dto = new ClientMessageDto.CreateStreamCompleted(msg.EventStreamId, msg.ErrorCode, msg.Error);
            return new TcpPackage(TcpCommand.CreateStreamCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.WriteEvents UnwrapWriteEvents(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.WriteEvents>();
            if (dto == null) return null;
            return new ClientMessage.WriteEvents(
                    package.CorrelationId,
                    envelope,
                    dto.AllowForwarding,
                    dto.EventStreamId,
                    dto.ExpectedVersion,
                    dto.Events.Select(x => new Event(new Guid(x.EventId), x.EventType, false,  x.Data, x.Metadata)).ToArray());
        }

        private static TcpPackage WrapWriteEvents(ClientMessage.WriteEvents msg)
        {
            var dto = new ClientMessageDto.WriteEvents(
                msg.EventStreamId,
                msg.ExpectedVersion,
                msg.Events.Select(x => new ClientMessageDto.Event(x.EventId, x.EventType, x.Data, x.Metadata)).ToArray(),
                msg.AllowForwarding);
            return new TcpPackage(TcpCommand.WriteEvents, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.WriteEventsCompleted UnwrapWriteEventCompleted(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.WriteEventsCompleted>();
            if (dto == null) return null;
            if (dto.ErrorCode == (int)OperationErrorCode.Success)
                return new ClientMessage.WriteEventsCompleted(package.CorrelationId, dto.EventStreamId, dto.EventNumber);

            return new ClientMessage.WriteEventsCompleted(package.CorrelationId,
                                                          dto.EventStreamId,
                                                          (OperationErrorCode) dto.ErrorCode,
                                                          dto.Error);
        }

        private static TcpPackage WrapWriteEventsCompleted(ClientMessage.WriteEventsCompleted msg)
        {
            var dto = new ClientMessageDto.WriteEventsCompleted(msg.EventStreamId,
                                                                msg.ErrorCode,
                                                                msg.Error,
                                                                msg.EventNumber);
            return new TcpPackage(TcpCommand.WriteEventsCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.TransactionStart UnwrapTransactionStart(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.TransactionStart>();
            if (dto == null) return null;
            return new ClientMessage.TransactionStart(package.CorrelationId, envelope, dto.AllowForwarding, dto.EventStreamId, dto.ExpectedVersion);
        }

        private static TcpPackage WrapTransactionStart(ClientMessage.TransactionStart msg)
        {
            var dto = new ClientMessageDto.TransactionStart(msg.EventStreamId, msg.ExpectedVersion, msg.AllowForwarding);
            return new TcpPackage(TcpCommand.TransactionStart, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.TransactionStartCompleted UnwrapTransactionStartCompleted(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.TransactionStartCompleted>();
            if (dto == null) return null;
            return new ClientMessage.TransactionStartCompleted(package.CorrelationId,
                                                               dto.TransactionId,
                                                               dto.EventStreamId,
                                                               (OperationErrorCode) dto.ErrorCode,
                                                               dto.Error);
        }

        private static TcpPackage WrapTransactionStartCompleted(ClientMessage.TransactionStartCompleted msg)
        {
            var dto = new ClientMessageDto.TransactionStartCompleted(msg.TransactionId, msg.EventStreamId, msg.ErrorCode, msg.Error);
            return new TcpPackage(TcpCommand.TransactionStartCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.TransactionWrite UnwrapTransactionWrite(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.TransactionWrite>();
            if (dto == null) return null;
            return new ClientMessage.TransactionWrite(
                package.CorrelationId,
                envelope,
                dto.AllowForwarding, 
                dto.TransactionId,
                dto.EventStreamId,
                dto.Events.Select(x => new Event(new Guid(x.EventId), x.EventType, false,  x.Data, x.Metadata)).ToArray());
        }

        private static TcpPackage WrapTransactionWrite(ClientMessage.TransactionWrite msg)
        {
            var dto = new ClientMessageDto.TransactionWrite(msg.TransactionId,
                    msg.EventStreamId,
                    msg.Events.Select(x => new ClientMessageDto.Event(x.EventId, x.EventType, x.Data, x.Metadata)).ToArray(),
                    msg.AllowForwarding);
            return new TcpPackage(TcpCommand.TransactionWrite, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.TransactionWriteCompleted UnwrapTransactionWriteCompleted(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.TransactionWriteCompleted>();
            if (dto == null) return null;
            return new ClientMessage.TransactionWriteCompleted(package.CorrelationId, dto.TransactionId, dto.EventStreamId, (OperationErrorCode)dto.ErrorCode, dto.Error);
        }

        private static TcpPackage WrapTransactionWriteCompleted(ClientMessage.TransactionWriteCompleted msg)
        {
            var dto = new ClientMessageDto.TransactionWriteCompleted(msg.TransactionId, msg.EventStreamId, msg.ErrorCode, msg.Error);
            return new TcpPackage(TcpCommand.TransactionWriteCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.TransactionCommit UnwrapTransactionCommit(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.TransactionCommit>();
            if (dto == null) return null;
            return new ClientMessage.TransactionCommit(package.CorrelationId, envelope, dto.AllowForwarding, dto.TransactionId, dto.EventStreamId);
        }

        private static TcpPackage WrapTransactionCommit(ClientMessage.TransactionCommit msg)
        {
            var dto = new ClientMessageDto.TransactionCommit(msg.TransactionId, msg.EventStreamId, msg.AllowForwarding);
            return new TcpPackage(TcpCommand.TransactionCommit, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.TransactionCommitCompleted UnwrapTransactionCommitCompleted(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.TransactionCommitCompleted>();
            if (dto == null) return null;
            return new ClientMessage.TransactionCommitCompleted(package.CorrelationId, dto.TransactionId, (OperationErrorCode)dto.ErrorCode, dto.Error);
        }

        private static TcpPackage WrapTransactionCommitCompleted(ClientMessage.TransactionCommitCompleted msg)
        {
            var dto = new ClientMessageDto.TransactionCommitCompleted(msg.TransactionId, msg.ErrorCode, msg.Error);
            return new TcpPackage(TcpCommand.TransactionCommitCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.DeleteStream UnwrapDeleteStream(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.DeleteStream>();
            if (dto == null) return null;
            return new ClientMessage.DeleteStream(package.CorrelationId, envelope, dto.AllowForwarding, dto.EventStreamId, dto.ExpectedVersion);
        }

        private static TcpPackage WrapDeleteStream(ClientMessage.DeleteStream msg)
        {
            var dto = new ClientMessageDto.DeleteStream(msg.EventStreamId, msg.ExpectedVersion, msg.AllowForwarding);
            return new TcpPackage(TcpCommand.DeleteStream, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.DeleteStreamCompleted UnwrapDeleteStreamCompleted(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.DeleteStreamCompleted>();
            if (dto == null) return null;
            return new ClientMessage.DeleteStreamCompleted(package.CorrelationId,
                                                           dto.EventStreamId,
                                                           (OperationErrorCode) dto.ErrorCode,
                                                           dto.Error);
        }

        private static TcpPackage WrapDeleteStreamCompleted(ClientMessage.DeleteStreamCompleted msg)
        {
            var dto = new ClientMessageDto.DeleteStreamCompleted(msg.EventStreamId, msg.ErrorCode, msg.Error);
            return new TcpPackage(TcpCommand.DeleteStreamCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.ReadEvent UnwrapReadEvents(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.ReadEvent>();
            if (dto == null) return null;
            return new ClientMessage.ReadEvent(package.CorrelationId, envelope, dto.EventStreamId, dto.EventNumber, dto.ResolveLinktos);
        }

        private static TcpPackage WrapReadEventsCompleted(ClientMessage.ReadEventCompleted msg)
        {
            var dto = new ClientMessageDto.ReadEventCompleted(msg.EventStreamId,
                                                              msg.EventNumber,
                                                              msg.Result,
                                                              msg.Record == null ? null : msg.Record.EventType,
                                                              msg.Record == null ? null : msg.Record.Data,
                                                              msg.Record == null ? null : msg.Record.Metadata,
                                                              msg.Record == null ? -1 : msg.Record.LogPosition);
            return new TcpPackage(TcpCommand.ReadEventCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.ReadStreamEventsForward UnwrapReadStreamEventsForward(TcpPackage package,
                                                                                           IEnvelope envelope,
                                                                                           TcpConnectionManager connection)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.ReadStreamEventsForward>();
            if (dto == null) return null;
            return new ClientMessage.ReadStreamEventsForward(package.CorrelationId,
                                                             envelope,
                                                             dto.EventStreamId,
                                                             dto.StartIndex,
                                                             dto.MaxCount,
                                                             dto.ResolveLinkTos,
                                                             dto.ReturnLastEventNumber);
        }

        private static TcpPackage WrapReadStreamEventsForwardCompleted(ClientMessage.ReadStreamEventsForwardCompleted msg)
        {
            var dto = new ClientMessageDto.ReadStreamEventsForwardCompleted(msg.EventStreamId,
                                                                            msg.Events,
                                                                            msg.Result,
                                                                            msg.LastCommitPosition,
                                                                            msg.LastEventNumber);
            return new TcpPackage(TcpCommand.ReadStreamEventsForwardCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.ReadStreamEventsBackward UnwrapReadStreamEventsBackward(TcpPackage package,
                                                                                             IEnvelope envelope,
                                                                                             TcpConnectionManager connection)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.ReadStreamEventsBackward>();
            if (dto == null) return null;
            return new ClientMessage.ReadStreamEventsBackward(package.CorrelationId,
                                                              envelope,
                                                              dto.EventStreamId,
                                                              dto.StartIndex,
                                                              dto.MaxCount,
                                                              dto.ResolveLinkTos);
        }

        private static TcpPackage WrapReadStreamEventsBackwardCompleted(ClientMessage.ReadStreamEventsBackwardCompleted msg)
        {
            var dto = new ClientMessageDto.ReadStreamEventsBackwardCompleted(msg.EventStreamId,
                                                                             msg.Events,
                                                                             msg.Result,
                                                                             msg.LastCommitPosition);
            return new TcpPackage(TcpCommand.ReadStreamEventsBackwardCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.ReadAllEventsForward UnwrapReadAllEventsForward(TcpPackage package, IEnvelope envelope, TcpConnectionManager connection)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.ReadAllEventsForward>();
            if (dto == null) return null;
            return new ClientMessage.ReadAllEventsForward(package.CorrelationId,
                                                          envelope,
                                                          dto.CommitPosition,
                                                          dto.PreparePosition,
                                                          dto.MaxCount,
                                                          dto.ResolveLinktos);
        }

        private static TcpPackage WrapReadAllEventsForwardCompleted(ClientMessage.ReadAllEventsForwardCompleted msg)
        {
            var dto = new ClientMessageDto.ReadAllEventsForwardCompleted(msg.Result.CurrentPos.CommitPosition,
                                                                         msg.Result.CurrentPos.PreparePosition,
                                                                         msg.Result.Records.Select(x => new EventLinkPair(x.Event, x.Link)).ToArray(),
                                                                         msg.Result.NextPos.CommitPosition,
                                                                         msg.Result.NextPos.PreparePosition);
            return new TcpPackage(TcpCommand.ReadAllEventsForwardCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.ReadAllEventsBackward UnwrapReadAllEventsBackward(TcpPackage package, IEnvelope envelope, TcpConnectionManager connection)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.ReadAllEventsBackward>();
            if (dto == null) return null;
            return new ClientMessage.ReadAllEventsBackward(package.CorrelationId,
                                                           envelope,
                                                           dto.CommitPosition,
                                                           dto.PreparePosition,
                                                           dto.MaxCount,
                                                           dto.ResolveLinkTos);
        }

        private static TcpPackage WrapReadAllEventsBackwardCompleted(ClientMessage.ReadAllEventsBackwardCompleted msg)
        {
            var dto = new ClientMessageDto.ReadAllEventsBackwardCompleted(msg.Result.CurrentPos.CommitPosition,
                                                                          msg.Result.CurrentPos.PreparePosition,
                                                                          msg.Result.Records.Select(x => new EventLinkPair(x.Event, x.Link)).ToArray(),
                                                                          msg.Result.NextPos.CommitPosition,
                                                                          msg.Result.NextPos.PreparePosition);
            return new TcpPackage(TcpCommand.ReadAllEventsBackwardCompleted, msg.CorrelationId, dto.Serialize());
        }


        private ClientMessage.SubscribeToStream UnwrapSubscribeToStream(TcpPackage package, IEnvelope envelope, TcpConnectionManager connection)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.SubscribeToStream>();
            if (dto == null) return null;
            return new ClientMessage.SubscribeToStream(connection, package.CorrelationId, dto.EventStreamId);
        }

        private ClientMessage.UnsubscribeFromStream UnwrapUnsubscribeFromStream(TcpPackage package, IEnvelope envelope, TcpConnectionManager connection)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.UnsubscribeFromStream>();
            if (dto == null) return null;
            return new ClientMessage.UnsubscribeFromStream(connection, package.CorrelationId, dto.EventStreamId);
        }

        private ClientMessage.SubscribeToAllStreams UnwrapSubscribeToAllStreams(TcpPackage package, IEnvelope envelope, TcpConnectionManager connection)
        {
            //var dto = package.Data.Deserialize<ClientMessageDto.SubscribeToAllStreams>();
            return new ClientMessage.SubscribeToAllStreams(connection, package.CorrelationId);
        }

        private ClientMessage.UnsubscribeFromAllStreams UnwrapUnsubscribeFromAllStreams(TcpPackage package, IEnvelope envelope, TcpConnectionManager connection)
        {
            //var dto = package.Data.Deserialize<ClientMessageDto.UnsubscribeFromAllStreams>();
            return new ClientMessage.UnsubscribeFromAllStreams(connection, package.CorrelationId);
        }

        private TcpPackage WrapStreamEventAppeared(ClientMessage.StreamEventAppeared msg)
        {
            var dto = new ClientMessageDto.StreamEventAppeared(msg.EventNumber, msg.Event);
            return new TcpPackage(TcpCommand.StreamEventAppeared, msg.CorrelationId, dto.Serialize());
        }

        private TcpPackage WrapSubscriptionDropped(ClientMessage.SubscriptionDropped msg)
        {
            var dto = new ClientMessageDto.SubscriptionDropped(msg.EventStreamId);
            return new TcpPackage(TcpCommand.SubscriptionDropped, msg.CorrelationId, dto.Serialize());
        }

        private TcpPackage WrapSubscriptionToAllDropped(ClientMessage.SubscriptionToAllDropped msg)
        {
            var dto = new ClientMessageDto.SubscriptionToAllDropped();
            return new TcpPackage(TcpCommand.SubscriptionToAllDropped, msg.CorrelationId, dto.Serialize());
        }

        private TcpPackage WrapDeniedToRoute(ClientMessage.DeniedToRoute msg)
        {
            var dto = new ClientMessageDto.DeniedToRoute(msg.TimeStamp,
                                                         msg.InternalTcpEndPoint,
                                                         msg.ExternalTcpEndPoint,
                                                         msg.InternalHttpEndPoint,
                                                         msg.ExternalHttpEndPoint);
            return new TcpPackage(TcpCommand.DeniedToRoute, msg.CorrelationId, dto.Serialize());
        }

        private SystemMessage.ScavengeDatabase UnwrapScavengeDatabase(TcpPackage package, IEnvelope envelope)
        {
            return new SystemMessage.ScavengeDatabase();
        }
    }
}