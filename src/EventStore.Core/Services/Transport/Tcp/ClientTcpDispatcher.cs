using System;
using System.Linq;
using System.Security.Principal;
using EventStore.Common.Utils;
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

            AddUnwrapper(TcpCommand.WriteEvents, UnwrapWriteEvents);
            AddWrapper<ClientMessage.WriteEvents>(WrapWriteEvents);
            AddUnwrapper(TcpCommand.WriteEventsCompleted, UnwrapWriteEventsCompleted);
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

            AddUnwrapper(TcpCommand.ReadEvent, UnwrapReadEvent);
            AddWrapper<ClientMessage.ReadEventCompleted>(WrapReadEventCompleted);

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
            AddUnwrapper(TcpCommand.CreatePersistentSubscription, UnwrapCreatePersistentSubscription);
            AddUnwrapper(TcpCommand.DeletePersistentSubscription, UnwrapDeletePersistentSubscription);
            AddWrapper<ClientMessage.CreatePersistentSubscriptionCompleted>(WrapCreatePersistentSubscriptionCompleted);
            AddWrapper<ClientMessage.DeletePersistentSubscriptionCompleted>(WrapDeletePersistentSubscriptionCompleted);


            AddUnwrapper(TcpCommand.ConnectToPersistentSubscription, UnwrapConnectToPersistentSubscription);
            AddUnwrapper(TcpCommand.PersistentSubscriptionAckEvents, UnwrapPersistentSubscriptionAckEvents);
            AddUnwrapper(TcpCommand.PersistentSubscriptionNakEvents, UnwrapPersistentSubscriptionNakEvents);
            AddWrapper<ClientMessage.PersistentSubscriptionConfirmation>(WrapPersistentSubscriptionConfirmation);
            AddWrapper<ClientMessage.PersistentSubscriptionStreamEventAppeared>(WrapPersistentSubscriptionStreamEventAppeared);

            AddUnwrapper(TcpCommand.ScavengeDatabase, UnwrapScavengeDatabase);
            AddWrapper<ClientMessage.ScavengeDatabaseCompleted>(WrapScavengeDatabaseResponse);
            
            AddWrapper<ClientMessage.NotHandled>(WrapNotHandled);
            AddUnwrapper(TcpCommand.NotHandled, UnwrapNotHandled);

            AddWrapper<TcpMessage.NotAuthenticated>(WrapNotAuthenticated);
            AddWrapper<TcpMessage.Authenticated>(WrapAuthenticated);
        }

        private static Message UnwrapPing(TcpPackage package, IEnvelope envelope)
        {
            var data = new byte[package.Data.Count];
            Buffer.BlockCopy(package.Data.Array, package.Data.Offset, data, 0, package.Data.Count);
            var pongMessage = new TcpMessage.PongMessage(package.CorrelationId, data);
            envelope.ReplyWith(pongMessage);
            return pongMessage;
        }

        private static TcpPackage WrapPong(TcpMessage.PongMessage message)
        {
            return new TcpPackage(TcpCommand.Pong, message.CorrelationId, message.Payload);
        }

        private static ClientMessage.WriteEvents UnwrapWriteEvents(TcpPackage package, IEnvelope envelope,
                                                                   IPrincipal user, string login, string password)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.WriteEvents>();
            if (dto == null) return null;
            
            var events = new Event[dto.Events == null ? 0 : dto.Events.Length];
            for (int i = 0; i < events.Length; ++i)
            {
// ReSharper disable PossibleNullReferenceException
                var e = dto.Events[i];
// ReSharper restore PossibleNullReferenceException
                events[i] = new Event(new Guid(e.EventId), e.EventType, e.DataContentType == 1, e.Data, e.Metadata);
            }
            return new ClientMessage.WriteEvents(Guid.NewGuid(), package.CorrelationId, envelope, dto.RequireMaster,
                                                 dto.EventStreamId, dto.ExpectedVersion, events, user, login, password);
        }

        private static TcpPackage WrapWriteEvents(ClientMessage.WriteEvents msg)
        {
            var events = new TcpClientMessageDto.NewEvent[msg.Events.Length];
            for (int i = 0; i < events.Length; ++i)
            {
                var e = msg.Events[i];
                events[i] = new TcpClientMessageDto.NewEvent(e.EventId.ToByteArray(), 
                                                             e.EventType, 
                                                             e.IsJson ? 1 : 0, 
                                                             0, e.Data, 
                                                             e.Metadata);
            }
            var dto = new TcpClientMessageDto.WriteEvents(msg.EventStreamId, msg.ExpectedVersion, events, msg.RequireMaster);
            return CreateWriteRequestPackage(TcpCommand.WriteEvents, msg, dto);
        }

        private static TcpPackage CreateWriteRequestPackage(TcpCommand command, ClientMessage.WriteRequestMessage msg, object dto)
        {
            // we forwarding with InternalCorrId, not client's CorrelationId!!!
            return msg.Login != null && msg.Password != null
                ? new TcpPackage(command, TcpFlags.Authenticated, msg.InternalCorrId, msg.Login, msg.Password, dto.Serialize())
                : new TcpPackage(command, TcpFlags.None, msg.InternalCorrId, null, null, dto.Serialize());
        }

        private static ClientMessage.WriteEventsCompleted UnwrapWriteEventsCompleted(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.WriteEventsCompleted>();
            if (dto == null) return null;
            if (dto.Result == TcpClientMessageDto.OperationResult.Success)
                return new ClientMessage.WriteEventsCompleted(package.CorrelationId, 
                                                             dto.FirstEventNumber, 
                                                             dto.LastEventNumber,
                                                             dto.PreparePosition ?? -1,
                                                             dto.CommitPosition ?? -1);
            return new ClientMessage.WriteEventsCompleted(package.CorrelationId, 
                                                          (OperationResult) dto.Result, 
                                                          dto.Message);
        }

        private static TcpPackage WrapWriteEventsCompleted(ClientMessage.WriteEventsCompleted msg)
        {
            var dto = new TcpClientMessageDto.WriteEventsCompleted((TcpClientMessageDto.OperationResult) msg.Result,
                                                                   msg.Message,
                                                                   msg.FirstEventNumber,
                                                                   msg.LastEventNumber,
                                                                   msg.PreparePosition,
                                                                   msg.CommitPosition);
            return new TcpPackage(TcpCommand.WriteEventsCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.TransactionStart UnwrapTransactionStart(TcpPackage package, IEnvelope envelope,
                                                                             IPrincipal user, string login, string password)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.TransactionStart>();
            if (dto == null) return null;
            return new ClientMessage.TransactionStart(Guid.NewGuid(), package.CorrelationId, envelope, dto.RequireMaster,
                                                      dto.EventStreamId, dto.ExpectedVersion, user, login, password);
        }

        private static TcpPackage WrapTransactionStart(ClientMessage.TransactionStart msg)
        {
            var dto = new TcpClientMessageDto.TransactionStart(msg.EventStreamId, msg.ExpectedVersion, msg.RequireMaster);
            return CreateWriteRequestPackage(TcpCommand.TransactionStart, msg, dto);
        }

        private static ClientMessage.TransactionStartCompleted UnwrapTransactionStartCompleted(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.TransactionStartCompleted>();
            if (dto == null) return null;
            return new ClientMessage.TransactionStartCompleted(package.CorrelationId, dto.TransactionId, (OperationResult) dto.Result, dto.Message);
        }

        private static TcpPackage WrapTransactionStartCompleted(ClientMessage.TransactionStartCompleted msg)
        {
            var dto = new TcpClientMessageDto.TransactionStartCompleted(msg.TransactionId, (TcpClientMessageDto.OperationResult) msg.Result, msg.Message);
            return new TcpPackage(TcpCommand.TransactionStartCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.TransactionWrite UnwrapTransactionWrite(TcpPackage package, IEnvelope envelope,
                                                                             IPrincipal user, string login, string password)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.TransactionWrite>();
            if (dto == null) return null;

            var events = new Event[dto.Events == null ? 0 : dto.Events.Length];
            for (int i = 0; i < events.Length; ++i)
            {
// ReSharper disable PossibleNullReferenceException
                var e = dto.Events[i];
// ReSharper restore PossibleNullReferenceException
                events[i] = new Event(new Guid(e.EventId), e.EventType, e.DataContentType == 1, e.Data, e.Metadata);
            }
            return new ClientMessage.TransactionWrite(Guid.NewGuid(), package.CorrelationId, envelope, dto.RequireMaster,
                                                      dto.TransactionId, events, user, login, password);
        }

        private static TcpPackage WrapTransactionWrite(ClientMessage.TransactionWrite msg)
        {
            var events = new TcpClientMessageDto.NewEvent[msg.Events.Length];
            for (int i = 0; i < events.Length; ++i)
            {
                var e = msg.Events[i];
                events[i] = new TcpClientMessageDto.NewEvent(e.EventId.ToByteArray(), e.EventType, e.IsJson ? 1 : 0, 0, e.Data, e.Metadata);
            }
            var dto = new TcpClientMessageDto.TransactionWrite(msg.TransactionId, events, msg.RequireMaster);
            return CreateWriteRequestPackage(TcpCommand.TransactionWrite, msg, dto);
        }

        private static ClientMessage.TransactionWriteCompleted UnwrapTransactionWriteCompleted(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.TransactionWriteCompleted>();
            if (dto == null) return null;
            return new ClientMessage.TransactionWriteCompleted(package.CorrelationId, dto.TransactionId, (OperationResult)dto.Result, dto.Message);
        }

        private static TcpPackage WrapTransactionWriteCompleted(ClientMessage.TransactionWriteCompleted msg)
        {
            var dto = new TcpClientMessageDto.TransactionWriteCompleted(msg.TransactionId, (TcpClientMessageDto.OperationResult) msg.Result, msg.Message);
            return new TcpPackage(TcpCommand.TransactionWriteCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.TransactionCommit UnwrapTransactionCommit(TcpPackage package, IEnvelope envelope,
                                                                               IPrincipal user, string login, string password)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.TransactionCommit>();
            if (dto == null) return null;
            return new ClientMessage.TransactionCommit(Guid.NewGuid(), package.CorrelationId, envelope, dto.RequireMaster,
                                                       dto.TransactionId, user, login, password);
        }

        private static TcpPackage WrapTransactionCommit(ClientMessage.TransactionCommit msg)
        {
            var dto = new TcpClientMessageDto.TransactionCommit(msg.TransactionId, msg.RequireMaster);
            return CreateWriteRequestPackage(TcpCommand.TransactionCommit, msg, dto);
        }

        private static ClientMessage.TransactionCommitCompleted UnwrapTransactionCommitCompleted(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.TransactionCommitCompleted>();
            if (dto == null) return null;
            if (dto.Result == TcpClientMessageDto.OperationResult.Success)
                return new ClientMessage.TransactionCommitCompleted(package.CorrelationId, dto.TransactionId, dto.FirstEventNumber, dto.LastEventNumber, dto.PreparePosition ?? -1, dto.CommitPosition ?? -1);
            return new ClientMessage.TransactionCommitCompleted(package.CorrelationId, dto.TransactionId, (OperationResult)dto.Result, dto.Message);
        }

        private static TcpPackage WrapTransactionCommitCompleted(ClientMessage.TransactionCommitCompleted msg)
        {
            var dto = new TcpClientMessageDto.TransactionCommitCompleted(msg.TransactionId, (TcpClientMessageDto.OperationResult) msg.Result,
                                                                         msg.Message, msg.FirstEventNumber, msg.LastEventNumber, msg.PreparePosition, msg.CommitPosition);
            return new TcpPackage(TcpCommand.TransactionCommitCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.DeleteStream UnwrapDeleteStream(TcpPackage package, IEnvelope envelope,
                                                                     IPrincipal user, string login, string password)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.DeleteStream>();
            if (dto == null) return null;
            return new ClientMessage.DeleteStream(Guid.NewGuid(), package.CorrelationId, envelope, dto.RequireMaster,
                                                  dto.EventStreamId, dto.ExpectedVersion, dto.HardDelete ?? false, user, login, password);
        }

        private static TcpPackage WrapDeleteStream(ClientMessage.DeleteStream msg)
        {
            var dto = new TcpClientMessageDto.DeleteStream(msg.EventStreamId, msg.ExpectedVersion, msg.RequireMaster, msg.HardDelete);
            return CreateWriteRequestPackage(TcpCommand.DeleteStream, msg, dto);
        }

        private static ClientMessage.DeleteStreamCompleted UnwrapDeleteStreamCompleted(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.DeleteStreamCompleted>();
            if (dto == null) return null;
            return new ClientMessage.DeleteStreamCompleted(package.CorrelationId, (OperationResult) dto.Result, 
                                                           dto.Message, 
                                                           dto.PreparePosition ?? -1,
                                                           dto.CommitPosition ?? -1); 
        }

        private static TcpPackage WrapDeleteStreamCompleted(ClientMessage.DeleteStreamCompleted msg)
        {
            var dto = new TcpClientMessageDto.DeleteStreamCompleted((TcpClientMessageDto.OperationResult) msg.Result, 
                                                                    msg.Message,
                                                                    msg.PreparePosition,
                                                                    msg.CommitPosition);
            return new TcpPackage(TcpCommand.DeleteStreamCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.ReadEvent UnwrapReadEvent(TcpPackage package, IEnvelope envelope, IPrincipal user)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.ReadEvent>();
            if (dto == null) return null;
            return new ClientMessage.ReadEvent(Guid.NewGuid(), package.CorrelationId, envelope, dto.EventStreamId,
                                               dto.EventNumber, dto.ResolveLinkTos, dto.RequireMaster, user);
        }

        private static TcpPackage WrapReadEventCompleted(ClientMessage.ReadEventCompleted msg)
        {
            var dto = new TcpClientMessageDto.ReadEventCompleted(
                (TcpClientMessageDto.ReadEventCompleted.ReadEventResult) msg.Result,
                new TcpClientMessageDto.ResolvedIndexedEvent(msg.Record.Event, msg.Record.Link), msg.Error);
            return new TcpPackage(TcpCommand.ReadEventCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.ReadStreamEventsForward UnwrapReadStreamEventsForward(TcpPackage package, IEnvelope envelope, IPrincipal user)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.ReadStreamEvents>();
            if (dto == null) return null;
            return new ClientMessage.ReadStreamEventsForward(Guid.NewGuid(), package.CorrelationId, envelope, 
                                                             dto.EventStreamId, dto.FromEventNumber, dto.MaxCount, 
                                                             dto.ResolveLinkTos, dto.RequireMaster, null, user);
        }

        private static TcpPackage WrapReadStreamEventsForwardCompleted(ClientMessage.ReadStreamEventsForwardCompleted msg)
        {
            var dto = new TcpClientMessageDto.ReadStreamEventsCompleted(
                ConvertToResolvedIndexedEvents(msg.Events), (TcpClientMessageDto.ReadStreamEventsCompleted.ReadStreamResult) msg.Result,
                msg.NextEventNumber, msg.LastEventNumber, msg.IsEndOfStream, msg.TfLastCommitPosition, msg.Error);
            return new TcpPackage(TcpCommand.ReadStreamEventsForwardCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.ReadStreamEventsBackward UnwrapReadStreamEventsBackward(TcpPackage package, IEnvelope envelope, IPrincipal user)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.ReadStreamEvents>();
            if (dto == null) return null;
            return new ClientMessage.ReadStreamEventsBackward(Guid.NewGuid(), package.CorrelationId, envelope,
                                                              dto.EventStreamId, dto.FromEventNumber, dto.MaxCount,
                                                              dto.ResolveLinkTos, dto.RequireMaster, null, user);
        }

        private static TcpPackage WrapReadStreamEventsBackwardCompleted(ClientMessage.ReadStreamEventsBackwardCompleted msg)
        {
            var dto = new TcpClientMessageDto.ReadStreamEventsCompleted(
                ConvertToResolvedIndexedEvents(msg.Events), (TcpClientMessageDto.ReadStreamEventsCompleted.ReadStreamResult) msg.Result,
                msg.NextEventNumber, msg.LastEventNumber, msg.IsEndOfStream, msg.TfLastCommitPosition, msg.Error);
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

        private static ClientMessage.ReadAllEventsForward UnwrapReadAllEventsForward(TcpPackage package, IEnvelope envelope, IPrincipal user)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.ReadAllEvents>();
            if (dto == null) return null;
            return new ClientMessage.ReadAllEventsForward(Guid.NewGuid(), package.CorrelationId, envelope, 
                                                          dto.CommitPosition, dto.PreparePosition, dto.MaxCount, 
                                                          dto.ResolveLinkTos, dto.RequireMaster, null, user);
        }

        private static TcpPackage WrapReadAllEventsForwardCompleted(ClientMessage.ReadAllEventsForwardCompleted msg)
        {
            var dto = new TcpClientMessageDto.ReadAllEventsCompleted(
                msg.CurrentPos.CommitPosition, msg.CurrentPos.PreparePosition, ConvertToResolvedEvents(msg.Events), 
                msg.NextPos.CommitPosition, msg.NextPos.PreparePosition, 
                (TcpClientMessageDto.ReadAllEventsCompleted.ReadAllResult) msg.Result, msg.Error);
            return new TcpPackage(TcpCommand.ReadAllEventsForwardCompleted, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.ReadAllEventsBackward UnwrapReadAllEventsBackward(TcpPackage package, IEnvelope envelope, IPrincipal user)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.ReadAllEvents>();
            if (dto == null) return null;
            return new ClientMessage.ReadAllEventsBackward(Guid.NewGuid(), package.CorrelationId, envelope,
                                                           dto.CommitPosition, dto.PreparePosition, dto.MaxCount,
                                                           dto.ResolveLinkTos, dto.RequireMaster, null, user);
        }

        private static TcpPackage WrapReadAllEventsBackwardCompleted(ClientMessage.ReadAllEventsBackwardCompleted msg)
        {
            var dto = new TcpClientMessageDto.ReadAllEventsCompleted(
                msg.CurrentPos.CommitPosition, msg.CurrentPos.PreparePosition, ConvertToResolvedEvents(msg.Events),
                msg.NextPos.CommitPosition, msg.NextPos.PreparePosition, 
                (TcpClientMessageDto.ReadAllEventsCompleted.ReadAllResult) msg.Result, msg.Error);
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

        private ClientMessage.SubscribeToStream UnwrapSubscribeToStream(TcpPackage package,
                                                                        IEnvelope envelope,
                                                                        IPrincipal user,
                                                                        string login,
                                                                        string pass,
                                                                        TcpConnectionManager connection)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.SubscribeToStream>();
            if (dto == null) return null;
            return new ClientMessage.SubscribeToStream(Guid.NewGuid(), package.CorrelationId, envelope,
                                                       connection.ConnectionId, dto.EventStreamId, dto.ResolveLinkTos, user);
        }

        private ClientMessage.UnsubscribeFromStream UnwrapUnsubscribeFromStream(TcpPackage package, IEnvelope envelope, IPrincipal user)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.UnsubscribeFromStream>();
            if (dto == null) return null;
            return new ClientMessage.UnsubscribeFromStream(Guid.NewGuid(), package.CorrelationId, envelope, user);
        }

        private TcpPackage WrapSubscribedToStream(ClientMessage.SubscriptionConfirmation msg)
        {
            var dto = new TcpClientMessageDto.SubscriptionConfirmation(msg.LastCommitPosition, msg.LastEventNumber);
            return new TcpPackage(TcpCommand.SubscriptionConfirmation, msg.CorrelationId, dto.Serialize());
        }

        private ClientMessage.CreatePersistentSubscription UnwrapCreatePersistentSubscription(
            TcpPackage package, IEnvelope envelope, IPrincipal user, string username, string password,
            TcpConnectionManager connection) {
            var dto = package.Data.Deserialize<TcpClientMessageDto.CreatePersistentSubscription>();
            if(dto == null) return null;
            return new ClientMessage.CreatePersistentSubscription(Guid.NewGuid(), package.CorrelationId, envelope,
                            dto.EventStreamId, dto.SubscriptionGroupName, dto.ResolveLinkTos, dto.StartFromBeginning, user, username, password);
        }

        private ClientMessage.DeletePersistentSubscription UnwrapDeletePersistentSubscription(
            TcpPackage package, IEnvelope envelope, IPrincipal user, string username, string password,
            TcpConnectionManager connection) {
            var dto = package.Data.Deserialize<TcpClientMessageDto.CreatePersistentSubscription>();
            if(dto == null) return null;
            return new ClientMessage.DeletePersistentSubscription(Guid.NewGuid(), package.CorrelationId, envelope,
                            dto.EventStreamId, dto.SubscriptionGroupName, user);
        }

        private TcpPackage WrapDeletePersistentSubscriptionCompleted(ClientMessage.DeletePersistentSubscriptionCompleted msg) {
            var dto = new TcpClientMessageDto.DeletePersistentSubscriptionCompleted((TcpClientMessageDto.DeletePersistentSubscriptionCompleted.DeletePersistentSubscriptionResult) msg.Result, msg.Reason);
            return new TcpPackage(TcpCommand.DeletePersistentSubscriptionCompleted, msg.CorrelationId, dto.Serialize());
        }

        private TcpPackage WrapCreatePersistentSubscriptionCompleted(ClientMessage.CreatePersistentSubscriptionCompleted msg) {
            var dto = new TcpClientMessageDto.CreatePersistentSubscriptionCompleted((TcpClientMessageDto.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult) msg.Result, msg.Reason);
            return new TcpPackage(TcpCommand.CreatePersistentSubscriptionCompleted, msg.CorrelationId, dto.Serialize());
        }

        private ClientMessage.ConnectToPersistentSubscription UnwrapConnectToPersistentSubscription(
            TcpPackage package, IEnvelope envelope, IPrincipal user, string login, string pass,
            TcpConnectionManager connection)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.ConnectToPersistentSubscription>();
            if (dto == null) return null;
            return new ClientMessage.ConnectToPersistentSubscription(Guid.NewGuid(), package.CorrelationId, envelope,
                connection.ConnectionId, dto.SubscriptionId, dto.EventStreamId, dto.AllowedInFlightMessages,connection.RemoteEndPoint.ToString() ,user);
        }

        private ClientMessage.PersistentSubscriptionAckEvents UnwrapPersistentSubscriptionAckEvents(
            TcpPackage package, IEnvelope envelope, IPrincipal user, string login, string pass,
            TcpConnectionManager connection)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.PersistentSubscriptionAckEvents>();
            if (dto == null) return null;
            return new ClientMessage.PersistentSubscriptionAckEvents(
                Guid.NewGuid(), package.CorrelationId, envelope, dto.SubscriptionId,
                dto.ProcessedEventIds.Select(x => new Guid(x)).ToArray(), user);
        }

        private ClientMessage.PersistentSubscriptionNakEvents UnwrapPersistentSubscriptionNakEvents(
    TcpPackage package, IEnvelope envelope, IPrincipal user, string login, string pass,
    TcpConnectionManager connection)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.PersistentSubscriptionAckEvents>();
            if (dto == null) return null;
            return new ClientMessage.PersistentSubscriptionNakEvents(
                Guid.NewGuid(), package.CorrelationId, envelope, dto.SubscriptionId,
                dto.ProcessedEventIds.Select(x => new Guid(x)).ToArray(), user);
        }

        private TcpPackage WrapPersistentSubscriptionConfirmation(ClientMessage.PersistentSubscriptionConfirmation msg)
        {
            var dto = new TcpClientMessageDto.PersistentSubscriptionConfirmation(msg.LastCommitPosition, msg.LastEventNumber);
            return new TcpPackage(TcpCommand.PersistentSubscriptionConfirmation, msg.CorrelationId, dto.Serialize());
        }

        private TcpPackage WrapPersistentSubscriptionStreamEventAppeared(ClientMessage.PersistentSubscriptionStreamEventAppeared msg)
        {
            var dto = new TcpClientMessageDto.PersistentSubscriptionStreamEventAppeared(new TcpClientMessageDto.ResolvedIndexedEvent(msg.Event.OriginalEvent, msg.Event.Link));
            return new TcpPackage(TcpCommand.PersistentSubscriptionStreamEventAppeared, msg.CorrelationId, dto.Serialize());
        }

        private TcpPackage WrapStreamEventAppeared(ClientMessage.StreamEventAppeared msg)
        {
            var dto = new TcpClientMessageDto.StreamEventAppeared(new TcpClientMessageDto.ResolvedEvent(msg.Event));
            return new TcpPackage(TcpCommand.StreamEventAppeared, msg.CorrelationId, dto.Serialize());
        }

        private TcpPackage WrapSubscriptionDropped(ClientMessage.SubscriptionDropped msg)
        {
            var dto = new TcpClientMessageDto.SubscriptionDropped((TcpClientMessageDto.SubscriptionDropped.SubscriptionDropReason) msg.Reason);
            return new TcpPackage(TcpCommand.SubscriptionDropped, msg.CorrelationId, dto.Serialize());
        }

        private ClientMessage.ScavengeDatabase UnwrapScavengeDatabase(TcpPackage package, IEnvelope envelope, IPrincipal user)
        {
            return new ClientMessage.ScavengeDatabase(envelope, package.CorrelationId, user);
        }

        private TcpPackage WrapScavengeDatabaseResponse(ClientMessage.ScavengeDatabaseCompleted msg)
        {
            var dto = new TcpClientMessageDto.ScavengeDatabaseCompleted(
                (TcpClientMessageDto.ScavengeDatabaseCompleted.ScavengeResult)msg.Result, msg.Error,
                (int) msg.TotalTime.TotalMilliseconds, msg.TotalSpaceSaved);
            return new TcpPackage(TcpCommand.ScavengeDatabaseCompleted, msg.CorrelationId, dto.Serialize());
        }

        private ClientMessage.NotHandled UnwrapNotHandled(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.NotHandled>();
            if (dto == null) return null;
            return new ClientMessage.NotHandled(package.CorrelationId, dto.Reason, dto.AdditionalInfo);
        }

        private TcpPackage WrapNotHandled(ClientMessage.NotHandled msg)
        {
            var dto = new TcpClientMessageDto.NotHandled(msg.Reason, msg.AdditionalInfo == null ? null : msg.AdditionalInfo.SerializeToArray());
            return new TcpPackage(TcpCommand.NotHandled, msg.CorrelationId, dto.Serialize());
        }

        private TcpPackage WrapNotAuthenticated(TcpMessage.NotAuthenticated msg)
        {
            return new TcpPackage(TcpCommand.NotAuthenticated, msg.CorrelationId, Helper.UTF8NoBom.GetBytes(msg.Reason ?? string.Empty));
        }

        private TcpPackage WrapAuthenticated(TcpMessage.Authenticated msg)
        {
            return new TcpPackage(TcpCommand.Authenticated, msg.CorrelationId, Empty.ByteArray);
        }
    }
}