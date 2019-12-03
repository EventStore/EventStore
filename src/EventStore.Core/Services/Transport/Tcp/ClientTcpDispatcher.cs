using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Util;

namespace EventStore.Core.Services.Transport.Tcp {
	public enum ClientVersion : byte {
		V1 = 0,
		V2 = 1
	}

	public class ClientTcpDispatcher : TcpDispatcher {
		public ClientTcpDispatcher() {
			AddUnwrapper(TcpCommand.Ping, UnwrapPing, ClientVersion.V2);
			AddWrapper<TcpMessage.PongMessage>(WrapPong, ClientVersion.V2);

			AddUnwrapper(TcpCommand.IdentifyClient, UnwrapIdentifyClient, ClientVersion.V2);

			AddUnwrapper(TcpCommand.WriteEvents, UnwrapWriteEvents, ClientVersion.V2);
			AddWrapper<ClientMessage.WriteEvents>(WrapWriteEvents, ClientVersion.V2);
			AddUnwrapper(TcpCommand.WriteEventsCompleted, UnwrapWriteEventsCompleted, ClientVersion.V2);
			AddWrapper<ClientMessage.WriteEventsCompleted>(WrapWriteEventsCompleted, ClientVersion.V2);

			AddUnwrapper(TcpCommand.TransactionStart, UnwrapTransactionStart, ClientVersion.V2);
			AddWrapper<ClientMessage.TransactionStart>(WrapTransactionStart, ClientVersion.V2);
			AddUnwrapper(TcpCommand.TransactionStartCompleted, UnwrapTransactionStartCompleted, ClientVersion.V2);
			AddWrapper<ClientMessage.TransactionStartCompleted>(WrapTransactionStartCompleted, ClientVersion.V2);

			AddUnwrapper(TcpCommand.TransactionWrite, UnwrapTransactionWrite, ClientVersion.V2);
			AddWrapper<ClientMessage.TransactionWrite>(WrapTransactionWrite, ClientVersion.V2);
			AddUnwrapper(TcpCommand.TransactionWriteCompleted, UnwrapTransactionWriteCompleted, ClientVersion.V2);
			AddWrapper<ClientMessage.TransactionWriteCompleted>(WrapTransactionWriteCompleted, ClientVersion.V2);

			AddUnwrapper(TcpCommand.TransactionCommit, UnwrapTransactionCommit, ClientVersion.V2);
			AddWrapper<ClientMessage.TransactionCommit>(WrapTransactionCommit, ClientVersion.V2);
			AddUnwrapper(TcpCommand.TransactionCommitCompleted, UnwrapTransactionCommitCompleted, ClientVersion.V2);
			AddWrapper<ClientMessage.TransactionCommitCompleted>(WrapTransactionCommitCompleted, ClientVersion.V2);

			AddUnwrapper(TcpCommand.DeleteStream, UnwrapDeleteStream, ClientVersion.V2);
			AddWrapper<ClientMessage.DeleteStream>(WrapDeleteStream, ClientVersion.V2);
			AddUnwrapper(TcpCommand.DeleteStreamCompleted, UnwrapDeleteStreamCompleted, ClientVersion.V2);
			AddWrapper<ClientMessage.DeleteStreamCompleted>(WrapDeleteStreamCompleted, ClientVersion.V2);

			AddUnwrapper(TcpCommand.ReadEvent, UnwrapReadEvent, ClientVersion.V2);
			AddWrapper<ClientMessage.ReadEventCompleted>(WrapReadEventCompleted, ClientVersion.V2);

			AddUnwrapper(TcpCommand.ReadStreamEventsForward, UnwrapReadStreamEventsForward, ClientVersion.V2);
			AddWrapper<ClientMessage.ReadStreamEventsForwardCompleted>(WrapReadStreamEventsForwardCompleted,
				ClientVersion.V2);
			AddUnwrapper(TcpCommand.ReadStreamEventsBackward, UnwrapReadStreamEventsBackward, ClientVersion.V2);
			AddWrapper<ClientMessage.ReadStreamEventsBackwardCompleted>(WrapReadStreamEventsBackwardCompleted,
				ClientVersion.V2);

			AddUnwrapper(TcpCommand.ReadAllEventsForward, UnwrapReadAllEventsForward, ClientVersion.V2);
			AddWrapper<ClientMessage.ReadAllEventsForwardCompleted>(WrapReadAllEventsForwardCompleted,
				ClientVersion.V2);
			AddUnwrapper(TcpCommand.ReadAllEventsBackward, UnwrapReadAllEventsBackward, ClientVersion.V2);
			AddWrapper<ClientMessage.ReadAllEventsBackwardCompleted>(WrapReadAllEventsBackwardCompleted,
				ClientVersion.V2);

			AddUnwrapper(TcpCommand.FilteredReadAllEventsForward, UnwrapFilteredReadAllEventsForward, ClientVersion.V2);
			AddWrapper<ClientMessage.FilteredReadAllEventsForwardCompleted>(WrapFilteredReadAllEventsForwardCompleted,
				ClientVersion.V2);

			AddUnwrapper(TcpCommand.FilteredReadAllEventsBackward, UnwrapFilteredReadAllEventsBackward,
				ClientVersion.V2);
			AddWrapper<ClientMessage.FilteredReadAllEventsBackwardCompleted>(WrapFilteredReadAllEventsBackwardCompleted,
				ClientVersion.V2);

			AddUnwrapper(TcpCommand.SubscribeToStream, UnwrapSubscribeToStream, ClientVersion.V2);
			AddUnwrapper(TcpCommand.FilteredSubscribeToStream, UnwrapFilteredSubscribeToStream, ClientVersion.V2);
			AddUnwrapper(TcpCommand.UnsubscribeFromStream, UnwrapUnsubscribeFromStream, ClientVersion.V2);

			AddWrapper<ClientMessage.CheckpointReached>(WrapCheckpointReached, ClientVersion.V2);

			AddWrapper<ClientMessage.SubscriptionConfirmation>(WrapSubscribedToStream, ClientVersion.V2);
			AddWrapper<ClientMessage.StreamEventAppeared>(WrapStreamEventAppeared, ClientVersion.V2);
			AddWrapper<ClientMessage.SubscriptionDropped>(WrapSubscriptionDropped, ClientVersion.V2);
			AddUnwrapper(TcpCommand.CreatePersistentSubscription, UnwrapCreatePersistentSubscription, ClientVersion.V2);
			AddUnwrapper(TcpCommand.DeletePersistentSubscription, UnwrapDeletePersistentSubscription, ClientVersion.V2);
			AddWrapper<ClientMessage.CreatePersistentSubscriptionCompleted>(WrapCreatePersistentSubscriptionCompleted,
				ClientVersion.V2);
			AddWrapper<ClientMessage.DeletePersistentSubscriptionCompleted>(WrapDeletePersistentSubscriptionCompleted,
				ClientVersion.V2);
			AddUnwrapper(TcpCommand.UpdatePersistentSubscription, UnwrapUpdatePersistentSubscription, ClientVersion.V2);
			AddWrapper<ClientMessage.UpdatePersistentSubscriptionCompleted>(WrapUpdatePersistentSubscriptionCompleted,
				ClientVersion.V2);


			AddUnwrapper(TcpCommand.ConnectToPersistentSubscription, UnwrapConnectToPersistentSubscription,
				ClientVersion.V2);
			AddUnwrapper(TcpCommand.PersistentSubscriptionAckEvents, UnwrapPersistentSubscriptionAckEvents,
				ClientVersion.V2);
			AddUnwrapper(TcpCommand.PersistentSubscriptionNakEvents, UnwrapPersistentSubscriptionNackEvents,
				ClientVersion.V2);
			AddWrapper<ClientMessage.PersistentSubscriptionConfirmation>(WrapPersistentSubscriptionConfirmation,
				ClientVersion.V2);
			AddWrapper<ClientMessage.PersistentSubscriptionStreamEventAppeared>(
				WrapPersistentSubscriptionStreamEventAppeared, ClientVersion.V2);

			AddUnwrapper(TcpCommand.ScavengeDatabase, UnwrapScavengeDatabase, ClientVersion.V2);
			AddWrapper<ClientMessage.ScavengeDatabaseResponse>(WrapScavengeDatabaseResponse, ClientVersion.V2);

			AddWrapper<ClientMessage.NotHandled>(WrapNotHandled, ClientVersion.V2);
			AddUnwrapper(TcpCommand.NotHandled, UnwrapNotHandled, ClientVersion.V2);

			AddWrapper<TcpMessage.NotAuthenticated>(WrapNotAuthenticated, ClientVersion.V2);
			AddWrapper<TcpMessage.Authenticated>(WrapAuthenticated, ClientVersion.V2);

			// Version 1
			AddWrapper<ClientMessage.ReadStreamEventsForwardCompleted>(WrapReadStreamEventsForwardCompletedV1,
				ClientVersion.V1);
			AddWrapper<ClientMessage.ReadStreamEventsBackwardCompleted>(WrapReadStreamEventsBackwardCompletedV1,
				ClientVersion.V1);
			AddWrapper<ClientMessage.ReadAllEventsForwardCompleted>(WrapReadAllEventsForwardCompletedV1,
				ClientVersion.V1);
			AddWrapper<ClientMessage.ReadAllEventsBackwardCompleted>(WrapReadAllEventsBackwardCompletedV1,
				ClientVersion.V1);
			AddWrapper<ClientMessage.SubscriptionConfirmation>(WrapSubscribedToStreamV1, ClientVersion.V1);
			AddWrapper<ClientMessage.StreamEventAppeared>(WrapStreamEventAppearedV1, ClientVersion.V1);
			AddWrapper<ClientMessage.PersistentSubscriptionConfirmation>(WrapPersistentSubscriptionConfirmationV1,
				ClientVersion.V1);
			AddWrapper<ClientMessage.PersistentSubscriptionStreamEventAppeared>(
				WrapPersistentSubscriptionStreamEventAppearedV1, ClientVersion.V1);
		}

		private TcpPackage WrapCheckpointReached(ClientMessage.CheckpointReached msg) {
			var dto = new TcpClientMessageDto.CheckpointReached(msg.Position.Value.CommitPosition,
				msg.Position.Value.PreparePosition);
			return new TcpPackage(TcpCommand.CheckpointReached, msg.CorrelationId, dto.Serialize());
		}

		private static Message UnwrapPing(TcpPackage package, IEnvelope envelope) {
			var data = new byte[package.Data.Count];
			Buffer.BlockCopy(package.Data.Array, package.Data.Offset, data, 0, package.Data.Count);
			var pongMessage = new TcpMessage.PongMessage(package.CorrelationId, data);
			envelope.ReplyWith(pongMessage);
			return pongMessage;
		}

		private static Message UnwrapIdentifyClient(TcpPackage package, IEnvelope envelope) {
			var dto = package.Data.Deserialize<TcpClientMessageDto.IdentifyClient>();
			if (dto == null) return null;

			return new ClientMessage.IdentifyClient(package.CorrelationId, dto.Version, dto.ConnectionName);
		}

		private static TcpPackage WrapPong(TcpMessage.PongMessage message) {
			return new TcpPackage(TcpCommand.Pong, message.CorrelationId, message.Payload);
		}

		private static ClientMessage.WriteEvents UnwrapWriteEvents(TcpPackage package, IEnvelope envelope,
			IPrincipal user, string login, string password) {
			var dto = package.Data.Deserialize<TcpClientMessageDto.WriteEvents>();
			if (dto == null) return null;

			var events = new Event[dto.Events == null ? 0 : dto.Events.Length];
			for (int i = 0; i < events.Length; ++i) {
				// ReSharper disable PossibleNullReferenceException
				var e = dto.Events[i];
				// ReSharper restore PossibleNullReferenceException
				events[i] = new Event(new Guid(e.EventId), e.EventType, e.DataContentType == 1, e.Data, e.Metadata);
			}

			return new ClientMessage.WriteEvents(Guid.NewGuid(), package.CorrelationId, envelope, dto.RequireMaster,
				dto.EventStreamId, dto.ExpectedVersion, events, user, login, password);
		}

		private static TcpPackage WrapWriteEvents(ClientMessage.WriteEvents msg) {
			var events = new TcpClientMessageDto.NewEvent[msg.Events.Length];
			for (int i = 0; i < events.Length; ++i) {
				var e = msg.Events[i];
				events[i] = new TcpClientMessageDto.NewEvent(e.EventId.ToByteArray(),
					e.EventType,
					e.IsJson ? 1 : 0,
					0, e.Data,
					e.Metadata);
			}

			var dto = new TcpClientMessageDto.WriteEvents(msg.EventStreamId, msg.ExpectedVersion, events,
				msg.RequireMaster);
			return CreateWriteRequestPackage(TcpCommand.WriteEvents, msg, dto);
		}

		private static TcpPackage CreateWriteRequestPackage(TcpCommand command, ClientMessage.WriteRequestMessage msg,
			object dto) {
			// we forwarding with InternalCorrId, not client's CorrelationId!!!
			if (msg.User == UserManagement.SystemAccount.Principal) {
				return new TcpPackage(command, TcpFlags.TrustedWrite, msg.InternalCorrId, null, null, dto.Serialize());
			}

			return msg.Login != null && msg.Password != null
				? new TcpPackage(command, TcpFlags.Authenticated, msg.InternalCorrId, msg.Login, msg.Password,
					dto.Serialize())
				: new TcpPackage(command, TcpFlags.None, msg.InternalCorrId, null, null, dto.Serialize());
		}

		private static ClientMessage.WriteEventsCompleted UnwrapWriteEventsCompleted(TcpPackage package,
			IEnvelope envelope) {
			var dto = package.Data.Deserialize<TcpClientMessageDto.WriteEventsCompleted>();
			if (dto == null) return null;
			if (dto.Result == TcpClientMessageDto.OperationResult.Success)
				return new ClientMessage.WriteEventsCompleted(package.CorrelationId,
					dto.FirstEventNumber,
					dto.LastEventNumber,
					dto.PreparePosition ?? -1,
					dto.CommitPosition ?? -1);
			return new ClientMessage.WriteEventsCompleted(package.CorrelationId,
				(OperationResult)dto.Result,
				dto.Message,
				dto.CurrentVersion ?? -1);
		}

		private static TcpPackage WrapWriteEventsCompleted(ClientMessage.WriteEventsCompleted msg) {
			var dto = new TcpClientMessageDto.WriteEventsCompleted((TcpClientMessageDto.OperationResult)msg.Result,
				msg.Message,
				msg.FirstEventNumber,
				msg.LastEventNumber,
				msg.PreparePosition,
				msg.CommitPosition,
				msg.CurrentVersion);
			return new TcpPackage(TcpCommand.WriteEventsCompleted, msg.CorrelationId, dto.Serialize());
		}

		private static ClientMessage.TransactionStart UnwrapTransactionStart(TcpPackage package, IEnvelope envelope,
			IPrincipal user, string login, string password) {
			var dto = package.Data.Deserialize<TcpClientMessageDto.TransactionStart>();
			if (dto == null) return null;
			return new ClientMessage.TransactionStart(Guid.NewGuid(), package.CorrelationId, envelope,
				dto.RequireMaster,
				dto.EventStreamId, dto.ExpectedVersion, user, login, password);
		}

		private static TcpPackage WrapTransactionStart(ClientMessage.TransactionStart msg) {
			var dto = new TcpClientMessageDto.TransactionStart(msg.EventStreamId, msg.ExpectedVersion,
				msg.RequireMaster);
			return CreateWriteRequestPackage(TcpCommand.TransactionStart, msg, dto);
		}

		private static ClientMessage.TransactionStartCompleted UnwrapTransactionStartCompleted(TcpPackage package,
			IEnvelope envelope) {
			var dto = package.Data.Deserialize<TcpClientMessageDto.TransactionStartCompleted>();
			if (dto == null) return null;
			return new ClientMessage.TransactionStartCompleted(package.CorrelationId, dto.TransactionId,
				(OperationResult)dto.Result, dto.Message);
		}

		private static TcpPackage WrapTransactionStartCompleted(ClientMessage.TransactionStartCompleted msg) {
			var dto = new TcpClientMessageDto.TransactionStartCompleted(msg.TransactionId,
				(TcpClientMessageDto.OperationResult)msg.Result, msg.Message);
			return new TcpPackage(TcpCommand.TransactionStartCompleted, msg.CorrelationId, dto.Serialize());
		}

		private static ClientMessage.TransactionWrite UnwrapTransactionWrite(TcpPackage package, IEnvelope envelope,
			IPrincipal user, string login, string password) {
			var dto = package.Data.Deserialize<TcpClientMessageDto.TransactionWrite>();
			if (dto == null) return null;

			var events = new Event[dto.Events == null ? 0 : dto.Events.Length];
			for (int i = 0; i < events.Length; ++i) {
				// ReSharper disable PossibleNullReferenceException
				var e = dto.Events[i];
				// ReSharper restore PossibleNullReferenceException
				events[i] = new Event(new Guid(e.EventId), e.EventType, e.DataContentType == 1, e.Data, e.Metadata);
			}

			return new ClientMessage.TransactionWrite(Guid.NewGuid(), package.CorrelationId, envelope,
				dto.RequireMaster,
				dto.TransactionId, events, user, login, password);
		}

		private static TcpPackage WrapTransactionWrite(ClientMessage.TransactionWrite msg) {
			var events = new TcpClientMessageDto.NewEvent[msg.Events.Length];
			for (int i = 0; i < events.Length; ++i) {
				var e = msg.Events[i];
				events[i] = new TcpClientMessageDto.NewEvent(e.EventId.ToByteArray(), e.EventType, e.IsJson ? 1 : 0, 0,
					e.Data, e.Metadata);
			}

			var dto = new TcpClientMessageDto.TransactionWrite(msg.TransactionId, events, msg.RequireMaster);
			return CreateWriteRequestPackage(TcpCommand.TransactionWrite, msg, dto);
		}

		private static ClientMessage.TransactionWriteCompleted UnwrapTransactionWriteCompleted(TcpPackage package,
			IEnvelope envelope) {
			var dto = package.Data.Deserialize<TcpClientMessageDto.TransactionWriteCompleted>();
			if (dto == null) return null;
			return new ClientMessage.TransactionWriteCompleted(package.CorrelationId, dto.TransactionId,
				(OperationResult)dto.Result, dto.Message);
		}

		private static TcpPackage WrapTransactionWriteCompleted(ClientMessage.TransactionWriteCompleted msg) {
			var dto = new TcpClientMessageDto.TransactionWriteCompleted(msg.TransactionId,
				(TcpClientMessageDto.OperationResult)msg.Result, msg.Message);
			return new TcpPackage(TcpCommand.TransactionWriteCompleted, msg.CorrelationId, dto.Serialize());
		}

		private static ClientMessage.TransactionCommit UnwrapTransactionCommit(TcpPackage package, IEnvelope envelope,
			IPrincipal user, string login, string password) {
			var dto = package.Data.Deserialize<TcpClientMessageDto.TransactionCommit>();
			if (dto == null) return null;
			return new ClientMessage.TransactionCommit(Guid.NewGuid(), package.CorrelationId, envelope,
				dto.RequireMaster,
				dto.TransactionId, user, login, password);
		}

		private static TcpPackage WrapTransactionCommit(ClientMessage.TransactionCommit msg) {
			var dto = new TcpClientMessageDto.TransactionCommit(msg.TransactionId, msg.RequireMaster);
			return CreateWriteRequestPackage(TcpCommand.TransactionCommit, msg, dto);
		}

		private static ClientMessage.TransactionCommitCompleted UnwrapTransactionCommitCompleted(TcpPackage package,
			IEnvelope envelope) {
			var dto = package.Data.Deserialize<TcpClientMessageDto.TransactionCommitCompleted>();
			if (dto == null) return null;
			if (dto.Result == TcpClientMessageDto.OperationResult.Success)
				return new ClientMessage.TransactionCommitCompleted(package.CorrelationId, dto.TransactionId,
					dto.FirstEventNumber, dto.LastEventNumber, dto.PreparePosition ?? -1, dto.CommitPosition ?? -1);
			return new ClientMessage.TransactionCommitCompleted(package.CorrelationId, dto.TransactionId,
				(OperationResult)dto.Result, dto.Message);
		}

		private static TcpPackage WrapTransactionCommitCompleted(ClientMessage.TransactionCommitCompleted msg) {
			var dto = new TcpClientMessageDto.TransactionCommitCompleted(msg.TransactionId,
				(TcpClientMessageDto.OperationResult)msg.Result,
				msg.Message, msg.FirstEventNumber, msg.LastEventNumber, msg.PreparePosition, msg.CommitPosition);
			return new TcpPackage(TcpCommand.TransactionCommitCompleted, msg.CorrelationId, dto.Serialize());
		}

		private static ClientMessage.DeleteStream UnwrapDeleteStream(TcpPackage package, IEnvelope envelope,
			IPrincipal user, string login, string password) {
			var dto = package.Data.Deserialize<TcpClientMessageDto.DeleteStream>();
			if (dto == null) return null;
			return new ClientMessage.DeleteStream(Guid.NewGuid(), package.CorrelationId, envelope, dto.RequireMaster,
				dto.EventStreamId, dto.ExpectedVersion, dto.HardDelete ?? false, user, login, password);
		}

		private static TcpPackage WrapDeleteStream(ClientMessage.DeleteStream msg) {
			var dto = new TcpClientMessageDto.DeleteStream(msg.EventStreamId, msg.ExpectedVersion, msg.RequireMaster,
				msg.HardDelete);
			return CreateWriteRequestPackage(TcpCommand.DeleteStream, msg, dto);
		}

		private static ClientMessage.DeleteStreamCompleted UnwrapDeleteStreamCompleted(TcpPackage package,
			IEnvelope envelope) {
			var dto = package.Data.Deserialize<TcpClientMessageDto.DeleteStreamCompleted>();
			if (dto == null) return null;
			return new ClientMessage.DeleteStreamCompleted(package.CorrelationId, (OperationResult)dto.Result,
				dto.Message,
				dto.PreparePosition ?? -1,
				dto.CommitPosition ?? -1);
		}

		private static TcpPackage WrapDeleteStreamCompleted(ClientMessage.DeleteStreamCompleted msg) {
			var dto = new TcpClientMessageDto.DeleteStreamCompleted((TcpClientMessageDto.OperationResult)msg.Result,
				msg.Message,
				msg.PreparePosition,
				msg.CommitPosition);
			return new TcpPackage(TcpCommand.DeleteStreamCompleted, msg.CorrelationId, dto.Serialize());
		}

		private static ClientMessage.ReadEvent
			UnwrapReadEvent(TcpPackage package, IEnvelope envelope, IPrincipal user) {
			var dto = package.Data.Deserialize<TcpClientMessageDto.ReadEvent>();
			if (dto == null) return null;
			return new ClientMessage.ReadEvent(Guid.NewGuid(), package.CorrelationId, envelope, dto.EventStreamId,
				dto.EventNumber, dto.ResolveLinkTos, dto.RequireMaster, user);
		}

		private static TcpPackage WrapReadEventCompleted(ClientMessage.ReadEventCompleted msg) {
			var dto = new TcpClientMessageDto.ReadEventCompleted(
				(TcpClientMessageDto.ReadEventCompleted.ReadEventResult)msg.Result,
				new TcpClientMessageDto.ResolvedIndexedEvent(msg.Record.Event, msg.Record.Link), msg.Error);
			return new TcpPackage(TcpCommand.ReadEventCompleted, msg.CorrelationId, dto.Serialize());
		}

		private static ClientMessage.ReadStreamEventsForward UnwrapReadStreamEventsForward(TcpPackage package,
			IEnvelope envelope, IPrincipal user) {
			var dto = package.Data.Deserialize<TcpClientMessageDto.ReadStreamEvents>();
			if (dto == null) return null;
			return new ClientMessage.ReadStreamEventsForward(Guid.NewGuid(), package.CorrelationId, envelope,
				dto.EventStreamId, dto.FromEventNumber, dto.MaxCount,
				dto.ResolveLinkTos, dto.RequireMaster, null, user);
		}

		private static TcpPackage WrapReadStreamEventsForwardCompleted(
			ClientMessage.ReadStreamEventsForwardCompleted msg) {
			var dto = new TcpClientMessageDto.ReadStreamEventsCompleted(
				ConvertToResolvedIndexedEvents(msg.Events),
				(TcpClientMessageDto.ReadStreamEventsCompleted.ReadStreamResult)msg.Result,
				msg.NextEventNumber, msg.LastEventNumber, msg.IsEndOfStream, msg.TfLastCommitPosition, msg.Error);
			return new TcpPackage(TcpCommand.ReadStreamEventsForwardCompleted, msg.CorrelationId, dto.Serialize());
		}

		private static ClientMessage.ReadStreamEventsBackward UnwrapReadStreamEventsBackward(TcpPackage package,
			IEnvelope envelope, IPrincipal user) {
			var dto = package.Data.Deserialize<TcpClientMessageDto.ReadStreamEvents>();
			if (dto == null) return null;
			return new ClientMessage.ReadStreamEventsBackward(Guid.NewGuid(), package.CorrelationId, envelope,
				dto.EventStreamId, dto.FromEventNumber, dto.MaxCount,
				dto.ResolveLinkTos, dto.RequireMaster, null, user);
		}

		private static TcpPackage WrapReadStreamEventsBackwardCompleted(
			ClientMessage.ReadStreamEventsBackwardCompleted msg) {
			var dto = new TcpClientMessageDto.ReadStreamEventsCompleted(
				ConvertToResolvedIndexedEvents(msg.Events),
				(TcpClientMessageDto.ReadStreamEventsCompleted.ReadStreamResult)msg.Result,
				msg.NextEventNumber, msg.LastEventNumber, msg.IsEndOfStream, msg.TfLastCommitPosition, msg.Error);
			return new TcpPackage(TcpCommand.ReadStreamEventsBackwardCompleted, msg.CorrelationId, dto.Serialize());
		}

		private static TcpClientMessageDto.ResolvedIndexedEvent[]
			ConvertToResolvedIndexedEvents(ResolvedEvent[] events) {
			var result = new TcpClientMessageDto.ResolvedIndexedEvent[events.Length];
			for (int i = 0; i < events.Length; ++i) {
				result[i] = new TcpClientMessageDto.ResolvedIndexedEvent(events[i].Event, events[i].Link);
			}

			return result;
		}

		private static ClientMessage.ReadAllEventsForward UnwrapReadAllEventsForward(TcpPackage package,
			IEnvelope envelope, IPrincipal user) {
			var dto = package.Data.Deserialize<TcpClientMessageDto.ReadAllEvents>();
			if (dto == null) return null;

			return new ClientMessage.ReadAllEventsForward(Guid.NewGuid(), package.CorrelationId, envelope,
				dto.CommitPosition, dto.PreparePosition, dto.MaxCount,
				dto.ResolveLinkTos, dto.RequireMaster, null, user, null);
		}


		private static TcpPackage WrapReadAllEventsForwardCompleted(ClientMessage.ReadAllEventsForwardCompleted msg) {
			var dto = new TcpClientMessageDto.ReadAllEventsCompleted(
				msg.CurrentPos.CommitPosition, msg.CurrentPos.PreparePosition, ConvertToResolvedEvents(msg.Events),
				msg.NextPos.CommitPosition, msg.NextPos.PreparePosition,
				(TcpClientMessageDto.ReadAllEventsCompleted.ReadAllResult)msg.Result, msg.Error);
			return new TcpPackage(TcpCommand.ReadAllEventsForwardCompleted, msg.CorrelationId, dto.Serialize());
		}

		private static ClientMessage.ReadAllEventsBackward UnwrapReadAllEventsBackward(TcpPackage package,
			IEnvelope envelope, IPrincipal user) {
			var dto = package.Data.Deserialize<TcpClientMessageDto.ReadAllEvents>();
			if (dto == null) return null;
			return new ClientMessage.ReadAllEventsBackward(Guid.NewGuid(), package.CorrelationId, envelope,
				dto.CommitPosition, dto.PreparePosition, dto.MaxCount,
				dto.ResolveLinkTos, dto.RequireMaster, null, user);
		}

		private static TcpPackage WrapReadAllEventsBackwardCompleted(ClientMessage.ReadAllEventsBackwardCompleted msg) {
			var dto = new TcpClientMessageDto.ReadAllEventsCompleted(
				msg.CurrentPos.CommitPosition, msg.CurrentPos.PreparePosition, ConvertToResolvedEvents(msg.Events),
				msg.NextPos.CommitPosition, msg.NextPos.PreparePosition,
				(TcpClientMessageDto.ReadAllEventsCompleted.ReadAllResult)msg.Result, msg.Error);
			return new TcpPackage(TcpCommand.ReadAllEventsBackwardCompleted, msg.CorrelationId, dto.Serialize());
		}

		private static ClientMessage.FilteredReadAllEventsForward UnwrapFilteredReadAllEventsForward(TcpPackage package,
			IEnvelope envelope, IPrincipal user) {
			var dto = package.Data.Deserialize<TcpClientMessageDto.FilteredReadAllEvents>();
			if (dto == null) return null;

			IEventFilter eventFilter = EventFilter.Get(dto.Filter);

			int maxSearchWindow = dto.MaxCount;
			if (dto.MaxSearchWindow.HasValue) {
				maxSearchWindow = dto.MaxSearchWindow.GetValueOrDefault();
			}

			return new ClientMessage.FilteredReadAllEventsForward(Guid.NewGuid(), package.CorrelationId, envelope,
				dto.CommitPosition, dto.PreparePosition, dto.MaxCount,
				dto.ResolveLinkTos, dto.RequireMaster, maxSearchWindow, null, eventFilter, user, null);
		}

		private static TcpPackage WrapFilteredReadAllEventsForwardCompleted(
			ClientMessage.FilteredReadAllEventsForwardCompleted msg) {
			var dto = new TcpClientMessageDto.FilteredReadAllEventsCompleted(
				msg.CurrentPos.CommitPosition, msg.CurrentPos.PreparePosition, ConvertToResolvedEvents(msg.Events),
				msg.NextPos.CommitPosition, msg.NextPos.PreparePosition, msg.IsEndOfStream,
				(TcpClientMessageDto.FilteredReadAllEventsCompleted.FilteredReadAllResult)msg.Result, msg.Error);
			return new TcpPackage(TcpCommand.FilteredReadAllEventsForwardCompleted, msg.CorrelationId, dto.Serialize());
		}

		private static TcpClientMessageDto.ResolvedEvent[] ConvertToResolvedEvents(ResolvedEvent[] events) {
			var result = new TcpClientMessageDto.ResolvedEvent[events.Length];
			for (int i = 0; i < events.Length; ++i) {
				result[i] = new TcpClientMessageDto.ResolvedEvent(events[i]);
			}

			return result;
		}

		private static ClientMessage.FilteredReadAllEventsBackward UnwrapFilteredReadAllEventsBackward(
			TcpPackage package,
			IEnvelope envelope, IPrincipal user) {
			var dto = package.Data.Deserialize<TcpClientMessageDto.FilteredReadAllEvents>();
			if (dto == null) return null;

			IEventFilter eventFilter = EventFilter.Get(dto.Filter);

			int maxSearchWindow = dto.MaxCount;
			if (dto.MaxSearchWindow.HasValue) {
				maxSearchWindow = dto.MaxSearchWindow.GetValueOrDefault();
			}

			return new ClientMessage.FilteredReadAllEventsBackward(Guid.NewGuid(), package.CorrelationId, envelope,
				dto.CommitPosition, dto.PreparePosition, dto.MaxCount,
				dto.ResolveLinkTos, dto.RequireMaster, maxSearchWindow, null, eventFilter, user, null);
		}

		private static TcpPackage WrapFilteredReadAllEventsBackwardCompleted(
			ClientMessage.FilteredReadAllEventsBackwardCompleted msg) {
			var dto = new TcpClientMessageDto.FilteredReadAllEventsCompleted(
				msg.CurrentPos.CommitPosition, msg.CurrentPos.PreparePosition, ConvertToResolvedEvents(msg.Events),
				msg.NextPos.CommitPosition, msg.NextPos.PreparePosition, msg.IsEndOfStream,
				(TcpClientMessageDto.FilteredReadAllEventsCompleted.FilteredReadAllResult)msg.Result, msg.Error);
			return new TcpPackage(TcpCommand.FilteredReadAllEventsBackwardCompleted, msg.CorrelationId,
				dto.Serialize());
		}

		private ClientMessage.SubscribeToStream UnwrapSubscribeToStream(TcpPackage package,
			IEnvelope envelope,
			IPrincipal user,
			string login,
			string pass,
			TcpConnectionManager connection) {
			var dto = package.Data.Deserialize<TcpClientMessageDto.SubscribeToStream>();
			if (dto == null) return null;
			return new ClientMessage.SubscribeToStream(Guid.NewGuid(), package.CorrelationId, envelope,
				connection.ConnectionId, dto.EventStreamId, dto.ResolveLinkTos, user);
		}

		private ClientMessage.FilteredSubscribeToStream UnwrapFilteredSubscribeToStream(TcpPackage package,
			IEnvelope envelope,
			IPrincipal user,
			string login,
			string pass,
			TcpConnectionManager connection) {
			var dto = package.Data.Deserialize<TcpClientMessageDto.FilteredSubscribeToStream>();
			if (dto == null) return null;

			IEventFilter eventFilter = EventFilter.Get(dto.Filter);

			return new ClientMessage.FilteredSubscribeToStream(Guid.NewGuid(), package.CorrelationId, envelope,
				connection.ConnectionId, dto.EventStreamId, dto.ResolveLinkTos, user, eventFilter,
				dto.CheckpointInterval);
		}

		private ClientMessage.UnsubscribeFromStream UnwrapUnsubscribeFromStream(TcpPackage package, IEnvelope envelope,
			IPrincipal user) {
			var dto = package.Data.Deserialize<TcpClientMessageDto.UnsubscribeFromStream>();
			if (dto == null) return null;
			return new ClientMessage.UnsubscribeFromStream(Guid.NewGuid(), package.CorrelationId, envelope, user);
		}

		private TcpPackage WrapSubscribedToStream(ClientMessage.SubscriptionConfirmation msg) {
			var dto = new TcpClientMessageDto.SubscriptionConfirmation(msg.LastCommitPosition, msg.LastEventNumber);
			return new TcpPackage(TcpCommand.SubscriptionConfirmation, msg.CorrelationId, dto.Serialize());
		}

		private ClientMessage.CreatePersistentSubscription UnwrapCreatePersistentSubscription(
			TcpPackage package, IEnvelope envelope, IPrincipal user, string username, string password,
			TcpConnectionManager connection) {
			var dto = package.Data.Deserialize<TcpClientMessageDto.CreatePersistentSubscription>();
			if (dto == null) return null;

			var namedConsumerStrategy = dto.NamedConsumerStrategy;
			if (string.IsNullOrEmpty(namedConsumerStrategy)) {
				namedConsumerStrategy = dto.PreferRoundRobin
					? SystemConsumerStrategies.RoundRobin
					: SystemConsumerStrategies.DispatchToSingle;
			}

			return new ClientMessage.CreatePersistentSubscription(Guid.NewGuid(), package.CorrelationId, envelope,
				dto.EventStreamId, dto.SubscriptionGroupName, dto.ResolveLinkTos, dto.StartFrom,
				dto.MessageTimeoutMilliseconds,
				dto.RecordStatistics, dto.MaxRetryCount, dto.BufferSize, dto.LiveBufferSize,
				dto.ReadBatchSize, dto.CheckpointAfterTime, dto.CheckpointMinCount,
				dto.CheckpointMaxCount, dto.SubscriberMaxCount, namedConsumerStrategy,
				user, username, password);
		}

		private ClientMessage.UpdatePersistentSubscription UnwrapUpdatePersistentSubscription(
			TcpPackage package, IEnvelope envelope, IPrincipal user, string username, string password,
			TcpConnectionManager connection) {
			var dto = package.Data.Deserialize<TcpClientMessageDto.UpdatePersistentSubscription>();
			if (dto == null) return null;

			var namedConsumerStrategy = dto.NamedConsumerStrategy;
			if (string.IsNullOrEmpty(namedConsumerStrategy)) {
				namedConsumerStrategy = dto.PreferRoundRobin
					? SystemConsumerStrategies.RoundRobin
					: SystemConsumerStrategies.DispatchToSingle;
			}

			return new ClientMessage.UpdatePersistentSubscription(Guid.NewGuid(), package.CorrelationId, envelope,
				dto.EventStreamId, dto.SubscriptionGroupName, dto.ResolveLinkTos, dto.StartFrom,
				dto.MessageTimeoutMilliseconds,
				dto.RecordStatistics, dto.MaxRetryCount, dto.BufferSize, dto.LiveBufferSize,
				dto.ReadBatchSize, dto.CheckpointAfterTime, dto.CheckpointMinCount,
				dto.CheckpointMaxCount, dto.SubscriberMaxCount, namedConsumerStrategy,
				user, username, password);
		}

		private ClientMessage.DeletePersistentSubscription UnwrapDeletePersistentSubscription(
			TcpPackage package, IEnvelope envelope, IPrincipal user, string username, string password,
			TcpConnectionManager connection) {
			var dto = package.Data.Deserialize<TcpClientMessageDto.CreatePersistentSubscription>();
			if (dto == null) return null;
			return new ClientMessage.DeletePersistentSubscription(Guid.NewGuid(), package.CorrelationId, envelope,
				dto.EventStreamId, dto.SubscriptionGroupName, user);
		}

		private TcpPackage WrapDeletePersistentSubscriptionCompleted(
			ClientMessage.DeletePersistentSubscriptionCompleted msg) {
			var dto = new TcpClientMessageDto.DeletePersistentSubscriptionCompleted(
				(TcpClientMessageDto.DeletePersistentSubscriptionCompleted.DeletePersistentSubscriptionResult)msg
					.Result, msg.Reason);
			return new TcpPackage(TcpCommand.DeletePersistentSubscriptionCompleted, msg.CorrelationId, dto.Serialize());
		}

		private TcpPackage WrapCreatePersistentSubscriptionCompleted(
			ClientMessage.CreatePersistentSubscriptionCompleted msg) {
			var dto = new TcpClientMessageDto.CreatePersistentSubscriptionCompleted(
				(TcpClientMessageDto.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult)msg
					.Result, msg.Reason);
			return new TcpPackage(TcpCommand.CreatePersistentSubscriptionCompleted, msg.CorrelationId, dto.Serialize());
		}

		private TcpPackage WrapUpdatePersistentSubscriptionCompleted(
			ClientMessage.UpdatePersistentSubscriptionCompleted msg) {
			var dto = new TcpClientMessageDto.UpdatePersistentSubscriptionCompleted(
				(TcpClientMessageDto.UpdatePersistentSubscriptionCompleted.UpdatePersistentSubscriptionResult)msg
					.Result, msg.Reason);
			return new TcpPackage(TcpCommand.UpdatePersistentSubscriptionCompleted, msg.CorrelationId, dto.Serialize());
		}


		private ClientMessage.ConnectToPersistentSubscription UnwrapConnectToPersistentSubscription(
			TcpPackage package, IEnvelope envelope, IPrincipal user, string login, string pass,
			TcpConnectionManager connection) {
			var dto = package.Data.Deserialize<TcpClientMessageDto.ConnectToPersistentSubscription>();
			if (dto == null) return null;
			return new ClientMessage.ConnectToPersistentSubscription(Guid.NewGuid(), package.CorrelationId, envelope,
				connection.ConnectionId, dto.SubscriptionId, dto.EventStreamId, dto.AllowedInFlightMessages,
				connection.RemoteEndPoint.ToString(), user);
		}

		private ClientMessage.PersistentSubscriptionAckEvents UnwrapPersistentSubscriptionAckEvents(
			TcpPackage package, IEnvelope envelope, IPrincipal user, string login, string pass,
			TcpConnectionManager connection) {
			var dto = package.Data.Deserialize<TcpClientMessageDto.PersistentSubscriptionAckEvents>();
			if (dto == null) return null;
			return new ClientMessage.PersistentSubscriptionAckEvents(
				Guid.NewGuid(), package.CorrelationId, envelope, dto.SubscriptionId,
				dto.ProcessedEventIds.Select(x => new Guid(x)).ToArray(), user);
		}

		private ClientMessage.PersistentSubscriptionNackEvents UnwrapPersistentSubscriptionNackEvents(
			TcpPackage package, IEnvelope envelope, IPrincipal user, string login, string pass,
			TcpConnectionManager connection) {
			var dto = package.Data.Deserialize<TcpClientMessageDto.PersistentSubscriptionNakEvents>();
			if (dto == null) return null;
			return new ClientMessage.PersistentSubscriptionNackEvents(
				Guid.NewGuid(), package.CorrelationId, envelope, dto.SubscriptionId,
				dto.Message, (ClientMessage.PersistentSubscriptionNackEvents.NakAction)dto.Action,
				dto.ProcessedEventIds.Select(x => new Guid(x)).ToArray(), user);
		}

		private TcpPackage
			WrapPersistentSubscriptionConfirmation(ClientMessage.PersistentSubscriptionConfirmation msg) {
			var dto = new TcpClientMessageDto.PersistentSubscriptionConfirmation(msg.LastCommitPosition,
				msg.SubscriptionId, msg.LastEventNumber);
			return new TcpPackage(TcpCommand.PersistentSubscriptionConfirmation, msg.CorrelationId, dto.Serialize());
		}

		private TcpPackage WrapPersistentSubscriptionStreamEventAppeared(
			ClientMessage.PersistentSubscriptionStreamEventAppeared msg) {
			var dto = new TcpClientMessageDto.PersistentSubscriptionStreamEventAppeared(
				new TcpClientMessageDto.ResolvedIndexedEvent(msg.Event.Event, msg.Event.Link), msg.RetryCount);
			return new TcpPackage(TcpCommand.PersistentSubscriptionStreamEventAppeared, msg.CorrelationId,
				dto.Serialize());
		}

		private TcpPackage WrapStreamEventAppeared(ClientMessage.StreamEventAppeared msg) {
			var dto = new TcpClientMessageDto.StreamEventAppeared(new TcpClientMessageDto.ResolvedEvent(msg.Event));
			return new TcpPackage(TcpCommand.StreamEventAppeared, msg.CorrelationId, dto.Serialize());
		}

		private TcpPackage WrapSubscriptionDropped(ClientMessage.SubscriptionDropped msg) {
			var dto = new TcpClientMessageDto.SubscriptionDropped(
				(TcpClientMessageDto.SubscriptionDropped.SubscriptionDropReason)msg.Reason);
			return new TcpPackage(TcpCommand.SubscriptionDropped, msg.CorrelationId, dto.Serialize());
		}

		private ClientMessage.ScavengeDatabase UnwrapScavengeDatabase(TcpPackage package, IEnvelope envelope,
			IPrincipal user) {
			return new ClientMessage.ScavengeDatabase(envelope, package.CorrelationId, user, 0, 1);
		}

		private TcpPackage WrapScavengeDatabaseResponse(ClientMessage.ScavengeDatabaseResponse msg) {
			TcpClientMessageDto.ScavengeDatabaseResponse.ScavengeResult result;
			switch (msg.Result) {
				case ClientMessage.ScavengeDatabaseResponse.ScavengeResult.Started:
					result = TcpClientMessageDto.ScavengeDatabaseResponse.ScavengeResult.Started;
					break;
				case ClientMessage.ScavengeDatabaseResponse.ScavengeResult.Unauthorized:
					result = TcpClientMessageDto.ScavengeDatabaseResponse.ScavengeResult.Unauthorized;
					break;
				case ClientMessage.ScavengeDatabaseResponse.ScavengeResult.InProgress:
					result = TcpClientMessageDto.ScavengeDatabaseResponse.ScavengeResult.InProgress;
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			var dto = new TcpClientMessageDto.ScavengeDatabaseResponse(result, msg.ScavengeId);
			return new TcpPackage(TcpCommand.ScavengeDatabaseResponse, msg.CorrelationId, dto.Serialize());
		}

		private ClientMessage.NotHandled UnwrapNotHandled(TcpPackage package, IEnvelope envelope) {
			var dto = package.Data.Deserialize<TcpClientMessageDto.NotHandled>();
			if (dto == null) return null;
			return new ClientMessage.NotHandled(package.CorrelationId, dto.Reason, dto.AdditionalInfo);
		}

		private TcpPackage WrapNotHandled(ClientMessage.NotHandled msg) {
			var dto = new TcpClientMessageDto.NotHandled(msg.Reason,
				msg.AdditionalInfo == null ? null : msg.AdditionalInfo.SerializeToArray());
			return new TcpPackage(TcpCommand.NotHandled, msg.CorrelationId, dto.Serialize());
		}

		private TcpPackage WrapNotAuthenticated(TcpMessage.NotAuthenticated msg) {
			return new TcpPackage(TcpCommand.NotAuthenticated, msg.CorrelationId,
				Helper.UTF8NoBom.GetBytes(msg.Reason ?? string.Empty));
		}

		private TcpPackage WrapAuthenticated(TcpMessage.Authenticated msg) {
			return new TcpPackage(TcpCommand.Authenticated, msg.CorrelationId, Empty.ByteArray);
		}


		private static TcpPackage WrapReadStreamEventsForwardCompletedV1(
			ClientMessage.ReadStreamEventsForwardCompleted msg) {
			var dto = new TcpClientMessageDto.ReadStreamEventsCompleted(
				ConvertToResolvedIndexedEvents(msg.Events),
				(TcpClientMessageDto.ReadStreamEventsCompleted.ReadStreamResult)msg.Result,
				msg.NextEventNumber, StreamVersionConverter.Downgrade(msg.LastEventNumber), msg.IsEndOfStream,
				msg.TfLastCommitPosition, msg.Error);
			return new TcpPackage(TcpCommand.ReadStreamEventsForwardCompleted, msg.CorrelationId, dto.Serialize());
		}

		private static TcpPackage WrapReadStreamEventsBackwardCompletedV1(
			ClientMessage.ReadStreamEventsBackwardCompleted msg) {
			var dto = new TcpClientMessageDto.ReadStreamEventsCompleted(
				ConvertToResolvedIndexedEvents(msg.Events),
				(TcpClientMessageDto.ReadStreamEventsCompleted.ReadStreamResult)msg.Result,
				msg.NextEventNumber, StreamVersionConverter.Downgrade(msg.LastEventNumber), msg.IsEndOfStream,
				msg.TfLastCommitPosition, msg.Error);
			return new TcpPackage(TcpCommand.ReadStreamEventsBackwardCompleted, msg.CorrelationId, dto.Serialize());
		}

		private static TcpPackage WrapReadAllEventsForwardCompletedV1(ClientMessage.ReadAllEventsForwardCompleted msg) {
			var dto = new TcpClientMessageDto.ReadAllEventsCompleted(
				msg.CurrentPos.CommitPosition, msg.CurrentPos.PreparePosition, ConvertToResolvedEventsV1(msg.Events),
				msg.NextPos.CommitPosition, msg.NextPos.PreparePosition,
				(TcpClientMessageDto.ReadAllEventsCompleted.ReadAllResult)msg.Result, msg.Error);
			return new TcpPackage(TcpCommand.ReadAllEventsForwardCompleted, msg.CorrelationId, dto.Serialize());
		}

		private static TcpPackage
			WrapReadAllEventsBackwardCompletedV1(ClientMessage.ReadAllEventsBackwardCompleted msg) {
			var dto = new TcpClientMessageDto.ReadAllEventsCompleted(
				msg.CurrentPos.CommitPosition, msg.CurrentPos.PreparePosition, ConvertToResolvedEventsV1(msg.Events),
				msg.NextPos.CommitPosition, msg.NextPos.PreparePosition,
				(TcpClientMessageDto.ReadAllEventsCompleted.ReadAllResult)msg.Result, msg.Error);
			return new TcpPackage(TcpCommand.ReadAllEventsBackwardCompleted, msg.CorrelationId, dto.Serialize());
		}

		private TcpPackage WrapPersistentSubscriptionConfirmationV1(
			ClientMessage.PersistentSubscriptionConfirmation msg) {
			var dto = new TcpClientMessageDto.PersistentSubscriptionConfirmation(msg.LastCommitPosition,
				msg.SubscriptionId,
				msg.LastEventNumber == null
					? msg.LastEventNumber
					: StreamVersionConverter.Downgrade(msg.LastEventNumber.Value));
			return new TcpPackage(TcpCommand.PersistentSubscriptionConfirmation, msg.CorrelationId, dto.Serialize());
		}

		private TcpPackage WrapPersistentSubscriptionStreamEventAppearedV1(
			ClientMessage.PersistentSubscriptionStreamEventAppeared msg) {
			var dto = new TcpClientMessageDto.PersistentSubscriptionStreamEventAppeared(
				ConvertToResolvedIndexedEventV1(msg.Event), msg.RetryCount);
			return new TcpPackage(TcpCommand.PersistentSubscriptionStreamEventAppeared, msg.CorrelationId,
				dto.Serialize());
		}

		private TcpPackage WrapSubscribedToStreamV1(ClientMessage.SubscriptionConfirmation msg) {
			var dto = new TcpClientMessageDto.SubscriptionConfirmation(msg.LastCommitPosition,
				msg.LastEventNumber == null
					? msg.LastEventNumber
					: StreamVersionConverter.Downgrade(msg.LastEventNumber.Value));
			return new TcpPackage(TcpCommand.SubscriptionConfirmation, msg.CorrelationId, dto.Serialize());
		}

		private TcpPackage WrapStreamEventAppearedV1(ClientMessage.StreamEventAppeared msg) {
			var dto = new TcpClientMessageDto.StreamEventAppeared(ConvertToResolvedEventV1(msg.Event));
			return new TcpPackage(TcpCommand.StreamEventAppeared, msg.CorrelationId, dto.Serialize());
		}

		private static TcpClientMessageDto.ResolvedEvent[] ConvertToResolvedEventsV1(ResolvedEvent[] events) {
			var result = new TcpClientMessageDto.ResolvedEvent[events.Length];
			for (int i = 0; i < events.Length; ++i) {
				result[i] = ConvertToResolvedEventV1(events[i]);
			}

			return result;
		}

		private static TcpClientMessageDto.ResolvedEvent ConvertToResolvedEventV1(ResolvedEvent evnt) {
			TcpClientMessageDto.EventRecord eventRecord = null;
			TcpClientMessageDto.EventRecord linkRecord = null;
			if (evnt.Event != null) {
				eventRecord = new TcpClientMessageDto.EventRecord(evnt.Event,
					StreamVersionConverter.Downgrade(evnt.Event.EventNumber));
			}

			if (evnt.Link != null) {
				linkRecord = new TcpClientMessageDto.EventRecord(evnt.Link,
					StreamVersionConverter.Downgrade(evnt.Link.EventNumber));
			}

			return new TcpClientMessageDto.ResolvedEvent(eventRecord, linkRecord,
				evnt.OriginalPosition.Value.CommitPosition,
				evnt.OriginalPosition.Value.PreparePosition);
		}

		private static TcpClientMessageDto.ResolvedIndexedEvent ConvertToResolvedIndexedEventV1(ResolvedEvent evnt) {
			TcpClientMessageDto.EventRecord eventRecord = null;
			TcpClientMessageDto.EventRecord linkRecord = null;
			if (evnt.Event != null) {
				eventRecord = new TcpClientMessageDto.EventRecord(evnt.Event,
					StreamVersionConverter.Downgrade(evnt.Event.EventNumber));
			}

			if (evnt.Link != null) {
				linkRecord = new TcpClientMessageDto.EventRecord(evnt.Link,
					StreamVersionConverter.Downgrade(evnt.Link.EventNumber));
			}

			return new TcpClientMessageDto.ResolvedIndexedEvent(eventRecord, linkRecord);
		}
	}
}
