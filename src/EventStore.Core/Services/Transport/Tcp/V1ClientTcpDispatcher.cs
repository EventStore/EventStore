using System;
using System.Linq;
using System.Security.Principal;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Helpers;

namespace EventStore.Core.Services.Transport.Tcp
{
    public class V1ClientTcpDispatcher : TcpDispatcher
    {
        public V1ClientTcpDispatcher()
        {
            AddUnwrapper(TcpCommand.ConnectToPersistentSubscriptionV1, UnwrapConnectToPersistentSubscriptionV1);
            AddUnwrapper(TcpCommand.PersistentSubscriptionAckEventsV1, UnwrapPersistentSubscriptionAckEventsV1);
            AddUnwrapper(TcpCommand.PersistentSubscriptionNakEventsV1, UnwrapPersistentSubscriptionNackEventsV1);
            AddWrapper<ClientMessage.PersistentSubscriptionConfirmation>(WrapPersistentSubscriptionConfirmationV1);
            AddWrapper<ClientMessage.PersistentSubscriptionStreamEventAppeared>(WrapPersistentSubscriptionStreamEventAppearedV1);

            AddUnwrapper(TcpCommand.WriteEventsV1, UnwrapWriteEventsV1);
            AddWrapper<ClientMessage.WriteEvents>(WrapWriteEventsV1);
            AddUnwrapper(TcpCommand.WriteEventsCompletedV1, UnwrapWriteEventsCompletedV1);
            AddWrapper<ClientMessage.WriteEventsCompleted>(WrapWriteEventsCompletedV1);

            AddUnwrapper(TcpCommand.TransactionStartV1, UnwrapTransactionStartV1);
            AddWrapper<ClientMessage.TransactionStart>(WrapTransactionStartV1);
            AddUnwrapper(TcpCommand.TransactionStartCompletedV1, UnwrapTransactionStartCompletedV1);
            AddWrapper<ClientMessage.TransactionStartCompleted>(WrapTransactionStartCompletedV1);

            AddUnwrapper(TcpCommand.TransactionWriteV1, UnwrapTransactionWriteV1);
            AddWrapper<ClientMessage.TransactionWrite>(WrapTransactionWriteV1);
            AddUnwrapper(TcpCommand.TransactionWriteCompletedV1, UnwrapTransactionWriteCompletedV1);
            AddWrapper<ClientMessage.TransactionWriteCompleted>(WrapTransactionWriteCompletedV1);

            AddUnwrapper(TcpCommand.TransactionCommitV1, UnwrapTransactionCommitV1);
            AddWrapper<ClientMessage.TransactionCommit>(WrapTransactionCommitV1);
            AddUnwrapper(TcpCommand.TransactionCommitCompletedV1, UnwrapTransactionCommitCompletedV1);
            AddWrapper<ClientMessage.TransactionCommitCompleted>(WrapTransactionCommitCompletedV1);

            AddUnwrapper(TcpCommand.DeleteStreamV1, UnwrapDeleteStreamV1);
            AddWrapper<ClientMessage.DeleteStream>(WrapDeleteStreamV1);
            AddUnwrapper(TcpCommand.DeleteStreamCompletedV1, UnwrapDeleteStreamCompletedV1);
            AddWrapper<ClientMessage.DeleteStreamCompleted>(WrapDeleteStreamCompletedV1);

            AddUnwrapper(TcpCommand.ReadEventV1, UnwrapReadEventV1);
            AddWrapper<ClientMessage.ReadEventCompleted>(WrapReadEventCompletedV1);

            AddUnwrapper(TcpCommand.ReadStreamEventsForwardV1, UnwrapReadStreamEventsForwardV1);
            AddWrapper<ClientMessage.ReadStreamEventsForwardCompleted>(WrapReadStreamEventsForwardCompletedV1);
            AddUnwrapper(TcpCommand.ReadStreamEventsBackwardV1, UnwrapReadStreamEventsBackwardV1);
            AddWrapper<ClientMessage.ReadStreamEventsBackwardCompleted>(WrapReadStreamEventsBackwardCompletedV1);

            AddUnwrapper(TcpCommand.ReadAllEventsForwardV1, UnwrapReadAllEventsForwardV1);
            AddWrapper<ClientMessage.ReadAllEventsForwardCompleted>(WrapReadAllEventsForwardCompletedV1);
            AddUnwrapper(TcpCommand.ReadAllEventsBackwardV1, UnwrapReadAllEventsBackwardV1);
            AddWrapper<ClientMessage.ReadAllEventsBackwardCompleted>(WrapReadAllEventsBackwardCompletedV1);

            AddUnwrapper(TcpCommand.SubscribeToStreamV1, UnwrapSubscribeToStreamV1);
            AddUnwrapper(TcpCommand.UnsubscribeFromStreamV1, UnwrapUnsubscribeFromStreamV1);

            AddWrapper<ClientMessage.SubscriptionConfirmation>(WrapSubscribedToStreamV1);
            AddWrapper<ClientMessage.StreamEventAppeared>(WrapStreamEventAppearedV1);
            AddWrapper<ClientMessage.SubscriptionDropped>(WrapSubscriptionDroppedV1);
            AddUnwrapper(TcpCommand.CreatePersistentSubscriptionV1, UnwrapCreatePersistentSubscriptionV1);
            AddUnwrapper(TcpCommand.DeletePersistentSubscriptionV1, UnwrapDeletePersistentSubscriptionV1);
            AddWrapper<ClientMessage.CreatePersistentSubscriptionCompleted>(WrapCreatePersistentSubscriptionCompletedV1);
            AddWrapper<ClientMessage.DeletePersistentSubscriptionCompleted>(WrapDeletePersistentSubscriptionCompletedV1);
            AddUnwrapper(TcpCommand.UpdatePersistentSubscriptionV1, UnwrapUpdatePersistentSubscriptionV1);
            AddWrapper<ClientMessage.UpdatePersistentSubscriptionCompleted>(WrapUpdatePersistentSubscriptionCompletedV1);

            AddUnwrapper(TcpCommand.ConnectToPersistentSubscriptionV1, UnwrapConnectToPersistentSubscriptionV1);
            AddUnwrapper(TcpCommand.PersistentSubscriptionAckEventsV1, UnwrapPersistentSubscriptionAckEventsV1);
            AddUnwrapper(TcpCommand.PersistentSubscriptionNakEventsV1, UnwrapPersistentSubscriptionNackEventsV1);
            AddWrapper<ClientMessage.PersistentSubscriptionConfirmation>(WrapPersistentSubscriptionConfirmationV1);
            AddWrapper<ClientMessage.PersistentSubscriptionStreamEventAppeared>(WrapPersistentSubscriptionStreamEventAppearedV1);
        }

        private static ClientMessage.WriteEvents UnwrapWriteEventsV1(TcpPackage package, IEnvelope envelope,
                                                                   IPrincipal user, string login, string password)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.WriteEventsV1>();
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
                                                 dto.EventStreamId, dto.ExpectedVersion,
                                                 events, user, login, password);
        }

        private static TcpPackage WrapWriteEventsV1(ClientMessage.WriteEvents msg)
        {
            var events = new TcpClientMessageDto.NewEventV1[msg.Events.Length];
            for (int i = 0; i < events.Length; ++i)
            {
                var e = msg.Events[i];
                events[i] = new TcpClientMessageDto.NewEventV1(e.EventId.ToByteArray(),
                                                             e.EventType,
                                                             e.IsJson ? 1 : 0,
                                                             0, e.Data,
                                                             e.Metadata);
            }
            var dto = new TcpClientMessageDto.WriteEventsV1(msg.EventStreamId, ExpectedVersionConverter.ConvertTo32Bit(msg.ExpectedVersion),
                events, msg.RequireMaster);
            return CreateWriteRequestPackage(TcpCommand.WriteEventsV1, msg, dto);
        }

        private static ClientMessage.WriteEventsCompleted UnwrapWriteEventsCompletedV1(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.WriteEventsCompletedV1>();
            if (dto == null) return null;
            if (dto.Result == TcpClientMessageDto.OperationResult.Success)
                return new ClientMessage.WriteEventsCompleted(package.CorrelationId,
                                                             dto.FirstEventNumber,
                                                             dto.LastEventNumber,
                                                             dto.PreparePosition ?? -1,
                                                             dto.CommitPosition ?? -1);
            return new ClientMessage.WriteEventsCompleted(package.CorrelationId,
                                                          (OperationResult)dto.Result,
                                                          dto.Message);
        }

        private static TcpPackage WrapWriteEventsCompletedV1(ClientMessage.WriteEventsCompleted msg)
        {
            var dto = new TcpClientMessageDto.WriteEventsCompletedV1((TcpClientMessageDto.OperationResult)msg.Result,
                                                                   msg.Message,
                                                                   ExpectedVersionConverter.ConvertTo32Bit(msg.FirstEventNumber),
                                                                   ExpectedVersionConverter.ConvertTo32Bit(msg.LastEventNumber),
                                                                   msg.PreparePosition,
                                                                   msg.CommitPosition);
            return new TcpPackage(TcpCommand.WriteEventsCompletedV1, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.TransactionStart UnwrapTransactionStartV1(TcpPackage package, IEnvelope envelope,
                                                                             IPrincipal user, string login, string password)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.TransactionStartV1>();
            if (dto == null) return null;
            return new ClientMessage.TransactionStart(Guid.NewGuid(), package.CorrelationId, envelope, dto.RequireMaster,
                                                      dto.EventStreamId, dto.ExpectedVersion, 
                                                      user, login, password);
        }

        private static TcpPackage WrapTransactionStartV1(ClientMessage.TransactionStart msg)
        {
            var dto = new TcpClientMessageDto.TransactionStartV1(msg.EventStreamId, ExpectedVersionConverter.ConvertTo32Bit(msg.ExpectedVersion), msg.RequireMaster);
            return CreateWriteRequestPackage(TcpCommand.TransactionStartV1, msg, dto);
        }

        private static ClientMessage.TransactionStartCompleted UnwrapTransactionStartCompletedV1(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.TransactionStartCompletedV1>();
            if (dto == null) return null;
            return new ClientMessage.TransactionStartCompleted(package.CorrelationId, dto.TransactionId, (OperationResult)dto.Result, dto.Message);
        }

        private static TcpPackage WrapTransactionStartCompletedV1(ClientMessage.TransactionStartCompleted msg)
        {
            var dto = new TcpClientMessageDto.TransactionStartCompletedV1(msg.TransactionId, (TcpClientMessageDto.OperationResult)msg.Result, msg.Message);
            return new TcpPackage(TcpCommand.TransactionStartCompletedV1, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.TransactionWrite UnwrapTransactionWriteV1(TcpPackage package, IEnvelope envelope,
                                                                             IPrincipal user, string login, string password)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.TransactionWriteV1>();
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

        private static TcpPackage WrapTransactionWriteV1(ClientMessage.TransactionWrite msg)
        {
            var events = new TcpClientMessageDto.NewEventV1[msg.Events.Length];
            for (int i = 0; i < events.Length; ++i)
            {
                var e = msg.Events[i];
                events[i] = new TcpClientMessageDto.NewEventV1(e.EventId.ToByteArray(), e.EventType, e.IsJson ? 1 : 0, 0, e.Data, e.Metadata);
            }
            var dto = new TcpClientMessageDto.TransactionWriteV1(msg.TransactionId, events, msg.RequireMaster);
            return CreateWriteRequestPackage(TcpCommand.TransactionWriteV1, msg, dto);
        }

        private static ClientMessage.TransactionWriteCompleted UnwrapTransactionWriteCompletedV1(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.TransactionWriteCompletedV1>();
            if (dto == null) return null;
            return new ClientMessage.TransactionWriteCompleted(package.CorrelationId, dto.TransactionId, (OperationResult)dto.Result, dto.Message);
        }

        private static TcpPackage WrapTransactionWriteCompletedV1(ClientMessage.TransactionWriteCompleted msg)
        {
            var dto = new TcpClientMessageDto.TransactionWriteCompletedV1(msg.TransactionId, (TcpClientMessageDto.OperationResult)msg.Result, msg.Message);
            return new TcpPackage(TcpCommand.TransactionWriteCompletedV1, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.TransactionCommit UnwrapTransactionCommitV1(TcpPackage package, IEnvelope envelope,
                                                                               IPrincipal user, string login, string password)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.TransactionCommitV1>();
            if (dto == null) return null;
            return new ClientMessage.TransactionCommit(Guid.NewGuid(), package.CorrelationId, envelope, dto.RequireMaster,
                                                       dto.TransactionId, user, login, password);
        }

        private static TcpPackage WrapTransactionCommitV1(ClientMessage.TransactionCommit msg)
        {
            var dto = new TcpClientMessageDto.TransactionCommitV1(msg.TransactionId, msg.RequireMaster);
            return CreateWriteRequestPackage(TcpCommand.TransactionCommitV1, msg, dto);
        }

        private static ClientMessage.TransactionCommitCompleted UnwrapTransactionCommitCompletedV1(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.TransactionCommitCompletedV1>();
            if (dto == null) return null;
            if (dto.Result == TcpClientMessageDto.OperationResult.Success)
                return new ClientMessage.TransactionCommitCompleted(package.CorrelationId, dto.TransactionId, dto.FirstEventNumber,
                    dto.LastEventNumber, dto.PreparePosition ?? -1, dto.CommitPosition ?? -1);
            return new ClientMessage.TransactionCommitCompleted(package.CorrelationId, dto.TransactionId, (OperationResult)dto.Result, dto.Message);
        }

        private static TcpPackage WrapTransactionCommitCompletedV1(ClientMessage.TransactionCommitCompleted msg)
        {
            var dto = new TcpClientMessageDto.TransactionCommitCompletedV1(msg.TransactionId, (TcpClientMessageDto.OperationResult)msg.Result,
                                                                         msg.Message, ExpectedVersionConverter.ConvertTo32Bit(msg.FirstEventNumber), 
                                                                         ExpectedVersionConverter.ConvertTo32Bit(msg.LastEventNumber), msg.PreparePosition, msg.CommitPosition);
            return new TcpPackage(TcpCommand.TransactionCommitCompletedV1, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.DeleteStream UnwrapDeleteStreamV1(TcpPackage package, IEnvelope envelope,
                                                                     IPrincipal user, string login, string password)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.DeleteStreamV1>();
            if (dto == null) return null;
            return new ClientMessage.DeleteStream(Guid.NewGuid(), package.CorrelationId, envelope, dto.RequireMaster,
                                                  dto.EventStreamId, dto.ExpectedVersion, 
                                                  dto.HardDelete ?? false, user, login, password);
        }

        private static TcpPackage WrapDeleteStreamV1(ClientMessage.DeleteStream msg)
        {
            var dto = new TcpClientMessageDto.DeleteStreamV1(msg.EventStreamId, ExpectedVersionConverter.ConvertTo32Bit(msg.ExpectedVersion), msg.RequireMaster, msg.HardDelete);
            return CreateWriteRequestPackage(TcpCommand.DeleteStreamV1, msg, dto);
        }

        private static ClientMessage.DeleteStreamCompleted UnwrapDeleteStreamCompletedV1(TcpPackage package, IEnvelope envelope)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.DeleteStreamCompletedV1>();
            if (dto == null) return null;
            return new ClientMessage.DeleteStreamCompleted(package.CorrelationId, (OperationResult)dto.Result,
                                                           dto.Message,
                                                           dto.PreparePosition ?? -1,
                                                           dto.CommitPosition ?? -1);
        }

        private static TcpPackage WrapDeleteStreamCompletedV1(ClientMessage.DeleteStreamCompleted msg)
        {
            var dto = new TcpClientMessageDto.DeleteStreamCompletedV1((TcpClientMessageDto.OperationResult)msg.Result,
                                                                    msg.Message,
                                                                    msg.PreparePosition,
                                                                    msg.CommitPosition);
            return new TcpPackage(TcpCommand.DeleteStreamCompletedV1, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.ReadEvent UnwrapReadEventV1(TcpPackage package, IEnvelope envelope, IPrincipal user)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.ReadEventV1>();
            if (dto == null) return null;
            return new ClientMessage.ReadEvent(Guid.NewGuid(), package.CorrelationId, envelope, dto.EventStreamId,
                                               ExpectedVersionConverter.ConvertTo32Bit(dto.EventNumber), dto.ResolveLinkTos, dto.RequireMaster, user);
        }

        private static TcpPackage WrapReadEventCompletedV1(ClientMessage.ReadEventCompleted msg)
        {
            var dto = new TcpClientMessageDto.ReadEventCompletedV1(
                (TcpClientMessageDto.ReadEventCompletedV1.ReadEventResultV1)msg.Result,
                new TcpClientMessageDto.ResolvedIndexedEventV1(msg.Record.Event, msg.Record.Link), msg.Error);
            return new TcpPackage(TcpCommand.ReadEventCompletedV1, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.ReadStreamEventsForward UnwrapReadStreamEventsForwardV1(TcpPackage package, IEnvelope envelope, IPrincipal user)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.ReadStreamEventsV1>();
            if (dto == null) return null;
            return new ClientMessage.ReadStreamEventsForward(Guid.NewGuid(), package.CorrelationId, envelope,
                                                             dto.EventStreamId, dto.FromEventNumber, 
                                                             dto.MaxCount, dto.ResolveLinkTos, dto.RequireMaster, null, user);
        }

        private static TcpPackage WrapReadStreamEventsForwardCompletedV1(ClientMessage.ReadStreamEventsForwardCompleted msg)
        {
            var dto = new TcpClientMessageDto.ReadStreamEventsCompletedV1(
                ConvertToResolvedIndexedEvents(msg.Events), (TcpClientMessageDto.ReadStreamEventsCompletedV1.ReadStreamResultV1)msg.Result,
                ExpectedVersionConverter.ConvertTo32Bit(msg.NextEventNumber), ExpectedVersionConverter.ConvertTo32Bit(msg.LastEventNumber), msg.IsEndOfStream, 
                msg.TfLastCommitPosition, msg.Error);
            return new TcpPackage(TcpCommand.ReadStreamEventsForwardCompletedV1, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.ReadStreamEventsBackward UnwrapReadStreamEventsBackwardV1(TcpPackage package, IEnvelope envelope, IPrincipal user)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.ReadStreamEventsV1>();
            if (dto == null) return null;
            return new ClientMessage.ReadStreamEventsBackward(Guid.NewGuid(), package.CorrelationId, envelope,
                                                              dto.EventStreamId, dto.FromEventNumber, 
                                                              dto.MaxCount, dto.ResolveLinkTos, dto.RequireMaster, null, user);
        }

        private static TcpPackage WrapReadStreamEventsBackwardCompletedV1(ClientMessage.ReadStreamEventsBackwardCompleted msg)
        {
            var dto = new TcpClientMessageDto.ReadStreamEventsCompletedV1(
                ConvertToResolvedIndexedEvents(msg.Events), (TcpClientMessageDto.ReadStreamEventsCompletedV1.ReadStreamResultV1)msg.Result,
                ExpectedVersionConverter.ConvertTo32Bit(msg.NextEventNumber), ExpectedVersionConverter.ConvertTo32Bit(msg.LastEventNumber), msg.IsEndOfStream, msg.TfLastCommitPosition, msg.Error);
            return new TcpPackage(TcpCommand.ReadStreamEventsBackwardCompletedV1, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.ReadAllEventsForward UnwrapReadAllEventsForwardV1(TcpPackage package, IEnvelope envelope, IPrincipal user)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.ReadAllEventsV1>();
            if (dto == null) return null;
            return new ClientMessage.ReadAllEventsForward(Guid.NewGuid(), package.CorrelationId, envelope,
                                                          dto.CommitPosition, dto.PreparePosition, dto.MaxCount,
                                                          dto.ResolveLinkTos, dto.RequireMaster, null, user);
        }

        private static TcpPackage WrapReadAllEventsForwardCompletedV1(ClientMessage.ReadAllEventsForwardCompleted msg)
        {
            var dto = new TcpClientMessageDto.ReadAllEventsCompletedV1(
                msg.CurrentPos.CommitPosition, msg.CurrentPos.PreparePosition, ConvertToResolvedEvents(msg.Events),
                msg.NextPos.CommitPosition, msg.NextPos.PreparePosition,
                (TcpClientMessageDto.ReadAllEventsCompletedV1.ReadAllResultV1)msg.Result, msg.Error);
            return new TcpPackage(TcpCommand.ReadAllEventsForwardCompletedV1, msg.CorrelationId, dto.Serialize());
        }

        private static ClientMessage.ReadAllEventsBackward UnwrapReadAllEventsBackwardV1(TcpPackage package, IEnvelope envelope, IPrincipal user)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.ReadAllEventsV1>();
            if (dto == null) return null;
            return new ClientMessage.ReadAllEventsBackward(Guid.NewGuid(), package.CorrelationId, envelope,
                                                           dto.CommitPosition, dto.PreparePosition, dto.MaxCount,
                                                           dto.ResolveLinkTos, dto.RequireMaster, null, user);
        }

        private static TcpPackage WrapReadAllEventsBackwardCompletedV1(ClientMessage.ReadAllEventsBackwardCompleted msg)
        {
            var dto = new TcpClientMessageDto.ReadAllEventsCompletedV1(
                msg.CurrentPos.CommitPosition, msg.CurrentPos.PreparePosition, ConvertToResolvedEvents(msg.Events),
                msg.NextPos.CommitPosition, msg.NextPos.PreparePosition,
                (TcpClientMessageDto.ReadAllEventsCompletedV1.ReadAllResultV1)msg.Result, msg.Error);
            return new TcpPackage(TcpCommand.ReadAllEventsBackwardCompletedV1, msg.CorrelationId, dto.Serialize());
        }

        private static TcpClientMessageDto.ResolvedEventV1[] ConvertToResolvedEventsV1(ResolvedEvent[] events)
        {
            var result = new TcpClientMessageDto.ResolvedEventV1[events.Length];
            for (int i = 0; i < events.Length; ++i)
            {
                result[i] = new TcpClientMessageDto.ResolvedEventV1(events[i]);
            }
            return result;
        }

        private ClientMessage.SubscribeToStream UnwrapSubscribeToStreamV1(TcpPackage package,
                                                                        IEnvelope envelope,
                                                                        IPrincipal user,
                                                                        string login,
                                                                        string pass,
                                                                        TcpConnectionManager connection)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.SubscribeToStreamV1>();
            if (dto == null) return null;
            return new ClientMessage.SubscribeToStream(Guid.NewGuid(), package.CorrelationId, envelope,
                                                       connection.ConnectionId, dto.EventStreamId, dto.ResolveLinkTos, user);
        }

        private ClientMessage.UnsubscribeFromStream UnwrapUnsubscribeFromStreamV1(TcpPackage package, IEnvelope envelope, IPrincipal user)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.UnsubscribeFromStreamV1>();
            if (dto == null) return null;
            return new ClientMessage.UnsubscribeFromStream(Guid.NewGuid(), package.CorrelationId, envelope, user);
        }

        private TcpPackage WrapSubscribedToStreamV1(ClientMessage.SubscriptionConfirmation msg)
        {
            var dto = new TcpClientMessageDto.SubscriptionConfirmationV1(msg.LastCommitPosition, ExpectedVersionConverter.ConvertTo32Bit(msg.LastEventNumber));
            return new TcpPackage(TcpCommand.SubscriptionConfirmationV1, msg.CorrelationId, dto.Serialize());
        }

        private ClientMessage.CreatePersistentSubscription UnwrapCreatePersistentSubscriptionV1(
            TcpPackage package, IEnvelope envelope, IPrincipal user, string username, string password,
            TcpConnectionManager connection)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.CreatePersistentSubscriptionV1>();
            if (dto == null) return null;

            var namedConsumerStrategy = dto.NamedConsumerStrategy;
            if (string.IsNullOrEmpty(namedConsumerStrategy))
            {
                namedConsumerStrategy = dto.PreferRoundRobin
                  ? SystemConsumerStrategies.RoundRobin
                  : SystemConsumerStrategies.DispatchToSingle;
            }

            return new ClientMessage.CreatePersistentSubscription(Guid.NewGuid(), package.CorrelationId, envelope,
                            dto.EventStreamId, dto.SubscriptionGroupName, dto.ResolveLinkTos, 
                            dto.StartFrom, dto.MessageTimeoutMilliseconds,
                            dto.RecordStatistics, dto.MaxRetryCount, dto.BufferSize, dto.LiveBufferSize,
                            dto.ReadBatchSize, dto.CheckpointAfterTime, dto.CheckpointMinCount,
                            dto.CheckpointMaxCount, dto.SubscriberMaxCount, namedConsumerStrategy,
                            user, username, password);
        }

        private ClientMessage.UpdatePersistentSubscription UnwrapUpdatePersistentSubscriptionV1(
            TcpPackage package, IEnvelope envelope, IPrincipal user, string username, string password,
            TcpConnectionManager connection)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.UpdatePersistentSubscriptionV1>();
            if (dto == null) return null;

            var namedConsumerStrategy = dto.NamedConsumerStrategy;
            if (string.IsNullOrEmpty(namedConsumerStrategy))
            {
                namedConsumerStrategy = dto.PreferRoundRobin
                  ? SystemConsumerStrategies.RoundRobin
                  : SystemConsumerStrategies.DispatchToSingle;
            }

            return new ClientMessage.UpdatePersistentSubscription(Guid.NewGuid(), package.CorrelationId, envelope,
                dto.EventStreamId, dto.SubscriptionGroupName, dto.ResolveLinkTos,
                dto.StartFrom,
                dto.MessageTimeoutMilliseconds,
                dto.RecordStatistics, dto.MaxRetryCount, dto.BufferSize, dto.LiveBufferSize,
                dto.ReadBatchSize, dto.CheckpointAfterTime, dto.CheckpointMinCount,
                dto.CheckpointMaxCount, dto.SubscriberMaxCount, namedConsumerStrategy,
                user, username, password);
        }

        private ClientMessage.DeletePersistentSubscription UnwrapDeletePersistentSubscriptionV1(
            TcpPackage package, IEnvelope envelope, IPrincipal user, string username, string password,
            TcpConnectionManager connection)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.CreatePersistentSubscriptionV1>();
            if (dto == null) return null;
            return new ClientMessage.DeletePersistentSubscription(Guid.NewGuid(), package.CorrelationId, envelope,
                            dto.EventStreamId, dto.SubscriptionGroupName, user);
        }

        private TcpPackage WrapDeletePersistentSubscriptionCompletedV1(ClientMessage.DeletePersistentSubscriptionCompleted msg)
        {
            var dto = new TcpClientMessageDto.DeletePersistentSubscriptionCompletedV1((TcpClientMessageDto.DeletePersistentSubscriptionCompletedV1.DeletePersistentSubscriptionResultV1)msg.Result, msg.Reason);
            return new TcpPackage(TcpCommand.DeletePersistentSubscriptionCompletedV1, msg.CorrelationId, dto.Serialize());
        }

        private TcpPackage WrapCreatePersistentSubscriptionCompletedV1(ClientMessage.CreatePersistentSubscriptionCompleted msg)
        {
            var dto = new TcpClientMessageDto.CreatePersistentSubscriptionCompletedV1((TcpClientMessageDto.CreatePersistentSubscriptionCompletedV1.CreatePersistentSubscriptionResultV1)msg.Result, msg.Reason);
            return new TcpPackage(TcpCommand.CreatePersistentSubscriptionCompletedV1, msg.CorrelationId, dto.Serialize());
        }

        private TcpPackage WrapUpdatePersistentSubscriptionCompletedV1(ClientMessage.UpdatePersistentSubscriptionCompleted msg)
        {
            var dto = new TcpClientMessageDto.UpdatePersistentSubscriptionCompletedV1((TcpClientMessageDto.UpdatePersistentSubscriptionCompletedV1.UpdatePersistentSubscriptionResultV1)msg.Result, msg.Reason);
            return new TcpPackage(TcpCommand.UpdatePersistentSubscriptionCompletedV1, msg.CorrelationId, dto.Serialize());
        }


        private ClientMessage.ConnectToPersistentSubscription UnwrapConnectToPersistentSubscriptionV1(
            TcpPackage package, IEnvelope envelope, IPrincipal user, string login, string pass,
            TcpConnectionManager connection)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.ConnectToPersistentSubscriptionV1>();
            if (dto == null) return null;
            return new ClientMessage.ConnectToPersistentSubscription(Guid.NewGuid(), package.CorrelationId, envelope,
                connection.ConnectionId, dto.SubscriptionId, dto.EventStreamId, dto.AllowedInFlightMessages, connection.RemoteEndPoint.ToString(), user);
        }

        private ClientMessage.PersistentSubscriptionAckEvents UnwrapPersistentSubscriptionAckEventsV1(
            TcpPackage package, IEnvelope envelope, IPrincipal user, string login, string pass,
            TcpConnectionManager connection)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.PersistentSubscriptionAckEventsV1>();
            if (dto == null) return null;
            return new ClientMessage.PersistentSubscriptionAckEvents(
                Guid.NewGuid(), package.CorrelationId, envelope, dto.SubscriptionId,
                dto.ProcessedEventIds.Select(x => new Guid(x)).ToArray(), user);
        }

        private ClientMessage.PersistentSubscriptionNackEvents UnwrapPersistentSubscriptionNackEventsV1(
            TcpPackage package, IEnvelope envelope, IPrincipal user, string login, string pass,
            TcpConnectionManager connection)
        {
            var dto = package.Data.Deserialize<TcpClientMessageDto.PersistentSubscriptionNakEventsV1>();
            if (dto == null) return null;
            return new ClientMessage.PersistentSubscriptionNackEvents(
                Guid.NewGuid(), package.CorrelationId, envelope, dto.SubscriptionId,
                dto.Message, (ClientMessage.PersistentSubscriptionNackEvents.NakAction)dto.Action,
                dto.ProcessedEventIds.Select(x => new Guid(x)).ToArray(), user);
        }

        private TcpPackage WrapPersistentSubscriptionConfirmationV1(ClientMessage.PersistentSubscriptionConfirmation msg)
        {
            var dto = new TcpClientMessageDto.PersistentSubscriptionConfirmationV1(msg.LastCommitPosition, msg.SubscriptionId, ExpectedVersionConverter.ConvertTo32Bit(msg.LastEventNumber));
            return new TcpPackage(TcpCommand.PersistentSubscriptionConfirmationV1, msg.CorrelationId, dto.Serialize());
        }

        private TcpPackage WrapPersistentSubscriptionStreamEventAppearedV1(ClientMessage.PersistentSubscriptionStreamEventAppeared msg)
        {
            var dto = new TcpClientMessageDto.PersistentSubscriptionStreamEventAppearedV1(new TcpClientMessageDto.ResolvedIndexedEventV1(msg.Event.Event, msg.Event.Link));
            return new TcpPackage(TcpCommand.PersistentSubscriptionStreamEventAppearedV1, msg.CorrelationId, dto.Serialize());
        }

        private TcpPackage WrapStreamEventAppearedV1(ClientMessage.StreamEventAppeared msg)
        {
            var dto = new TcpClientMessageDto.StreamEventAppearedV1(new TcpClientMessageDto.ResolvedEventV1(msg.Event));
            return new TcpPackage(TcpCommand.StreamEventAppearedV1, msg.CorrelationId, dto.Serialize());
        }

        private TcpPackage WrapSubscriptionDroppedV1(ClientMessage.SubscriptionDropped msg)
        {
            var dto = new TcpClientMessageDto.SubscriptionDroppedV1((TcpClientMessageDto.SubscriptionDroppedV1.SubscriptionDropReasonV1)msg.Reason);
            return new TcpPackage(TcpCommand.SubscriptionDroppedV1, msg.CorrelationId, dto.Serialize());
        }

        private ClientMessage.ScavengeDatabase UnwrapScavengeDatabaseV1(TcpPackage package, IEnvelope envelope, IPrincipal user)
        {
            return new ClientMessage.ScavengeDatabase(envelope, package.CorrelationId, user);
        }

        private static TcpPackage CreateWriteRequestPackage(TcpCommand command, ClientMessage.WriteRequestMessage msg, object dto)
        {
            // we forwarding with InternalCorrId, not client's CorrelationId!!!
            if (msg.User == UserManagement.SystemAccount.Principal)
            {
                return new TcpPackage(command, TcpFlags.TrustedWrite, msg.InternalCorrId, null, null, dto.Serialize());
            }
            return msg.Login != null && msg.Password != null
                ? new TcpPackage(command, TcpFlags.Authenticated, msg.InternalCorrId, msg.Login, msg.Password, dto.Serialize())
                : new TcpPackage(command, TcpFlags.None, msg.InternalCorrId, null, null, dto.Serialize());
        }

        private static TcpClientMessageDto.ResolvedIndexedEventV1[] ConvertToResolvedIndexedEvents(ResolvedEvent[] events)
        {
            var result = new TcpClientMessageDto.ResolvedIndexedEventV1[events.Length];
            for (int i = 0; i < events.Length; ++i)
            {
                result[i] = new TcpClientMessageDto.ResolvedIndexedEventV1(events[i].Event, events[i].Link);
            }
            return result;
        }

        private static TcpClientMessageDto.ResolvedEventV1[] ConvertToResolvedEvents(ResolvedEvent[] events)
        {
            var result = new TcpClientMessageDto.ResolvedEventV1[events.Length];
            for (int i = 0; i < events.Length; ++i)
            {
                result[i] = new TcpClientMessageDto.ResolvedEventV1(events[i]);
            }
            return result;
        }

    }
}