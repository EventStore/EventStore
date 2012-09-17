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

            AddUnwrapper(TcpCommand.ReadEventsForward, UnwrapReadEventsForward);
            AddWrapper<ClientMessage.ReadEventsForwardCompleted>(WrapReadEventsForwardCompleted);

            AddUnwrapper(TcpCommand.SubscribeToStream, UnwrapSubscribeToStream);
            AddUnwrapper(TcpCommand.UnsubscribeFromStream, UnwrapUnsubscribeFromStream);

            AddUnwrapper(TcpCommand.SubscribeToAllStreams, UnwrapSubscribeToAllStreams);
            AddUnwrapper(TcpCommand.UnsubscribeFromAllStreams, UnwrapUnsubscribeFromAllStreams);

            AddWrapper<ClientMessage.StreamEventAppeared>(WrapStreamEventAppeared);
            AddWrapper<ClientMessage.SubscriptionDropped>(WrapSubscriptionDropped);
            AddWrapper<ClientMessage.SubscriptionToAllDropped>(WrapSubscriptionToAllDropped);

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
            return new ClientMessage.CreateStream(new Guid(dto.CorrelationId), envelope, dto.EventStreamId, dto.Metadata);
        }

        private static TcpPackage WrapCreateStream(ClientMessage.CreateStream msg)
        {
            var dto = new ClientMessageDto.CreateStream(msg.CorrelationId, msg.EventStreamId, msg.Metadata);
            return new TcpPackage(TcpCommand.CreateStream, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.CreateStreamCompleted UnwrapCreateStreamCompleted(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.CreateStreamCompleted>();
            return new ClientMessage.CreateStreamCompleted(new Guid(dto.CorrelationId), dto.EventStreamId, (OperationErrorCode)dto.ErrorCode, dto.Error);
        }

        private static TcpPackage WrapCreateStreamCompleted(ClientMessage.CreateStreamCompleted msg)
        {
            var dto = new ClientMessageDto.CreateStreamCompleted(msg.CorrelationId, msg.EventStreamId, msg.ErrorCode, msg.Error);
            return new TcpPackage(TcpCommand.CreateStreamCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.WriteEvents UnwrapWriteEvents(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.WriteEvents>();
            return new ClientMessage.WriteEvents(
                    new Guid(dto.CorrelationId),
                    envelope,
                    dto.EventStreamId,
                    dto.ExpectedVersion,
                    dto.Events.Select(x => new Event(new Guid(x.EventId), x.EventType, false,  x.Data, x.Metadata)).ToArray());
        }

        private static TcpPackage WrapWriteEvents(ClientMessage.WriteEvents msg)
        {
            var dto = new ClientMessageDto.WriteEvents(
                msg.CorrelationId,
                msg.EventStreamId,
                msg.ExpectedVersion,
                msg.Events.Select(x => new ClientMessageDto.Event(x.EventId, x.EventType, x.Data, x.Metadata)).ToArray());
            return new TcpPackage(TcpCommand.WriteEvents, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.WriteEventsCompleted UnwrapWriteEventCompleted(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.WriteEventsCompleted>();
            
            if (dto.ErrorCode == (int)OperationErrorCode.Success)
                return new ClientMessage.WriteEventsCompleted(new Guid(dto.CorrelationId), dto.EventStreamId, dto.EventNumber);

            return new ClientMessage.WriteEventsCompleted(new Guid(dto.CorrelationId),
                                                          dto.EventStreamId,
                                                          (OperationErrorCode) dto.ErrorCode,
                                                          dto.Error);
        }

        private static TcpPackage WrapWriteEventsCompleted(ClientMessage.WriteEventsCompleted msg)
        {
            var dto = new ClientMessageDto.WriteEventsCompleted(msg.CorrelationId,
                                                                msg.EventStreamId,
                                                                msg.ErrorCode,
                                                                msg.Error,
                                                                msg.EventNumber);
            return new TcpPackage(TcpCommand.WriteEventsCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.TransactionStart UnwrapTransactionStart(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.TransactionStart>();
            return new ClientMessage.TransactionStart(new Guid(dto.CorrelationId), envelope, dto.EventStreamId, dto.ExpectedVersion);
        }

        private static TcpPackage WrapTransactionStart(ClientMessage.TransactionStart msg)
        {
            var dto = new ClientMessageDto.TransactionStart(msg.CorrelationId, msg.EventStreamId, msg.ExpectedVersion);
            return new TcpPackage(TcpCommand.TransactionStart, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.TransactionStartCompleted UnwrapTransactionStartCompleted(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.TransactionStartCompleted>();
            return new ClientMessage.TransactionStartCompleted(new Guid(dto.CorrelationId),
                                                               dto.TransactionId,
                                                               dto.EventStreamId,
                                                               (OperationErrorCode) dto.ErrorCode,
                                                               dto.Error);
        }

        private static TcpPackage WrapTransactionStartCompleted(ClientMessage.TransactionStartCompleted msg)
        {
            var dto = new ClientMessageDto.TransactionStartCompleted(msg.CorrelationId, msg.TransactionId, msg.EventStreamId, msg.ErrorCode, msg.Error);
            return new TcpPackage(TcpCommand.TransactionStartCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.TransactionWrite UnwrapTransactionWrite(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.TransactionWrite>();
            return new ClientMessage.TransactionWrite(
                new Guid(dto.CorrelationId),
                envelope,
                dto.TransactionId,
                dto.EventStreamId,
                dto.Events.Select(x => new Event(new Guid(x.EventId), x.EventType, false,  x.Data, x.Metadata)).ToArray());
        }

        private static TcpPackage WrapTransactionWrite(ClientMessage.TransactionWrite msg)
        {
            var dto = new ClientMessageDto.TransactionWrite(
                    msg.CorrelationId,
                    msg.TransactionId,
                    msg.EventStreamId,
                    msg.Events.Select(x => new ClientMessageDto.Event(x.EventId, x.EventType, x.Data, x.Metadata)).ToArray());
            return new TcpPackage(TcpCommand.TransactionWrite, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.TransactionWriteCompleted UnwrapTransactionWriteCompleted(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.TransactionWriteCompleted>();
            return new ClientMessage.TransactionWriteCompleted(new Guid(dto.CorrelationId), dto.TransactionId, dto.EventStreamId, (OperationErrorCode)dto.ErrorCode, dto.Error);
        }

        private static TcpPackage WrapTransactionWriteCompleted(ClientMessage.TransactionWriteCompleted msg)
        {
            var dto = new ClientMessageDto.TransactionWriteCompleted(msg.CorrelationId, msg.TransactionId, msg.EventStreamId, msg.ErrorCode, msg.Error);
            return new TcpPackage(TcpCommand.TransactionWriteCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.TransactionCommit UnwrapTransactionCommit(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.TransactionCommit>();
            return new ClientMessage.TransactionCommit(new Guid(dto.CorrelationId), envelope, dto.TransactionId, dto.EventStreamId);
        }

        private static TcpPackage WrapTransactionCommit(ClientMessage.TransactionCommit msg)
        {
            var dto = new ClientMessageDto.TransactionCommit(msg.CorrelationId, msg.TransactionId, msg.EventStreamId);
            return new TcpPackage(TcpCommand.TransactionCommit, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.TransactionCommitCompleted UnwrapTransactionCommitCompleted(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.TransactionCommitCompleted>();
            return new ClientMessage.TransactionCommitCompleted(new Guid(dto.CorrelationId), dto.TransactionId, (OperationErrorCode)dto.ErrorCode, dto.Error);
        }

        private static TcpPackage WrapTransactionCommitCompleted(ClientMessage.TransactionCommitCompleted msg)
        {
            var dto = new ClientMessageDto.TransactionCommitCompleted(msg.CorrelationId, msg.TransactionId, msg.ErrorCode, msg.Error);
            return new TcpPackage(TcpCommand.TransactionCommitCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.DeleteStream UnwrapDeleteStream(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.DeleteStream>();
            return new ClientMessage.DeleteStream(new Guid(dto.CorrelationId), envelope, dto.EventStreamId, dto.ExpectedVersion);
        }

        private static TcpPackage WrapDeleteStream(ClientMessage.DeleteStream msg)
        {
            var dto = new ClientMessageDto.DeleteStream(msg.CorrelationId, msg.EventStreamId, msg.ExpectedVersion);
            return new TcpPackage(TcpCommand.DeleteStream, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.DeleteStreamCompleted UnwrapDeleteStreamCompleted(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.DeleteStreamCompleted>();
            return new ClientMessage.DeleteStreamCompleted(new Guid(dto.CorrelationId),
                                                           dto.EventStreamId,
                                                           (OperationErrorCode) dto.ErrorCode,
                                                           dto.Error);
        }

        private static TcpPackage WrapDeleteStreamCompleted(ClientMessage.DeleteStreamCompleted msg)
        {
            var dto = new ClientMessageDto.DeleteStreamCompleted(msg.CorrelationId, msg.EventStreamId, msg.ErrorCode, msg.Error);
            return new TcpPackage(TcpCommand.DeleteStreamCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.ReadEvent UnwrapReadEvents(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.ReadEvent>();
            return new ClientMessage.ReadEvent(new Guid(dto.CorrelationId), envelope, dto.EventStreamId, dto.EventNumber, dto.ResolveLinktos);
        }

        private static TcpPackage WrapReadEventsCompleted(ClientMessage.ReadEventCompleted msg)
        {
            var dto = new ClientMessageDto.ReadEventCompleted(msg.CorrelationId,
                                                              msg.EventStreamId,
                                                              msg.EventNumber,
                                                              msg.Result,
                                                              msg.Record == null ? null : msg.Record.EventType,
                                                              msg.Record == null ? null : msg.Record.Data,
                                                              msg.Record == null ? null : msg.Record.Metadata);
            return new TcpPackage(TcpCommand.ReadEventCompleted, msg.CorrelationId, dto.Serialize());
        }

        //private static TcpPackage WrapReadEventsFromBeginning(ClientMessage.ReadEventsForward msg)
        //{
        //    var dto = new ClientMessageDto.ReadEventsForward(msg.CorrelationId,
        //                                                           msg.EventStreamId,
        //                                                           msg.EventNumber,
        //                                                           msg.MaxCount);
        //    return new TcpPackage(TcpCommand.ReadEventsForward, dto.Serialize());
        //}

        private static ClientMessage.ReadEventsForward UnwrapReadEventsForward(TcpPackage package,
                                                                               IEnvelope envelope,
                                                                               TcpConnectionManager connection)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.ReadEventsForward>();

            return new ClientMessage.ReadEventsForward(new Guid(dto.CorrelationId),
                                                             envelope,
                                                             dto.EventStreamId,
                                                             dto.StartIndex,
                                                             dto.MaxCount,
                                                             dto.ResolveLinktos);
        }

        private static TcpPackage WrapReadEventsForwardCompleted(ClientMessage.ReadEventsForwardCompleted msg)
        {
            var dto = new ClientMessageDto.ReadEventsForwardCompleted(msg.CorrelationId,
                                                                      msg.EventStreamId,
                                                                      msg.Events,
                                                                      msg.LinkToEvents,
                                                                      msg.Result,
                                                                      msg.LastCommitPosition);
            return new TcpPackage(TcpCommand.ReadEventsFromBeginningCompleted, msg.CorrelationId, dto.Serialize());
        }

        //private static ClientMessage.ReadEventsForwardCompleted UnwrapReadEventsFromBeginningCompleted(
        //                                                                            TcpPackage package, 
        //                                                                            IEnvelope envelope, 
        //                                                                            TcpConnectionManager connection)
        //{
        //    var dto = package.Data.Deserialize<ClientMessageDto.ReadEventsForwardCompleted>();

        //    return new ClientMessage.ReadEventsForwardCompleted(new Guid(dto.CorrelationId),
        //                                                              dto.EventStreamId,
        //                                                              dto.Events,
        //                                                              dto.LinkToEvents,
        //                                                              (RangeReadResult)dto.Result,
        //                                                              dto.LastCommitPosition);
        //}

        private ClientMessage.SubscribeToStream UnwrapSubscribeToStream(TcpPackage package, IEnvelope envelope, TcpConnectionManager connection)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.SubscribeToStream>();
            return new ClientMessage.SubscribeToStream(connection, new Guid(dto.CorrelationId), dto.EventStreamId);
        }

        private ClientMessage.UnsubscribeFromStream UnwrapUnsubscribeFromStream(TcpPackage package, IEnvelope envelope, TcpConnectionManager connection)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.UnsubscribeFromStream>();
            return new ClientMessage.UnsubscribeFromStream(connection, new Guid(dto.CorrelationId), dto.EventStreamId);
        }

        private ClientMessage.SubscribeToAllStreams UnwrapSubscribeToAllStreams(TcpPackage package, IEnvelope envelope, TcpConnectionManager connection)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.SubscribeToAllStreams>();
            return new ClientMessage.SubscribeToAllStreams(connection, new Guid(dto.CorrelationId));
        }

        private ClientMessage.UnsubscribeFromAllStreams UnwrapUnsubscribeFromAllStreams(TcpPackage package, IEnvelope envelope, TcpConnectionManager connection)
        {
            var dto = package.Data.Deserialize<ClientMessageDto.UnsubscribeFromAllStreams>();
            return new ClientMessage.UnsubscribeFromAllStreams(connection, new Guid(dto.CorrelationId));
        }

        private TcpPackage WrapStreamEventAppeared(ClientMessage.StreamEventAppeared msg)
        {
            var dto = new ClientMessageDto.StreamEventAppeared(msg.CorrelationId, msg.EventNumber, msg.Event);
            return new TcpPackage(TcpCommand.StreamEventAppeared, msg.CorrelationId, dto.Serialize());
        }

        private TcpPackage WrapSubscriptionDropped(ClientMessage.SubscriptionDropped msg)
        {
            var dto = new ClientMessageDto.SubscriptionDropped(msg.CorrelationId, msg.EventStreamId);
            return new TcpPackage(TcpCommand.SubscriptionDropped, msg.CorrelationId, dto.Serialize());
        }

        private TcpPackage WrapSubscriptionToAllDropped(ClientMessage.SubscriptionToAllDropped msg)
        {
            var dto = new ClientMessageDto.SubscriptionToAllDropped(msg.CorrelationId);
            return new TcpPackage(TcpCommand.SubscriptionToAllDropped, msg.CorrelationId, dto.Serialize());
        }

        private SystemMessage.ScavengeDatabase UnwrapScavengeDatabase(TcpPackage package, IEnvelope envelope)
        {
            return new SystemMessage.ScavengeDatabase();
        }
    }
}