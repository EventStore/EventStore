// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using EventStore.Client.Messages;
using EventStore.Common.Utils;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Util;
using static EventStore.Core.Messages.ClientMessage;
using CheckpointReached = EventStore.Client.Messages.CheckpointReached;
using FilteredSubscribeToStream = EventStore.Client.Messages.FilteredSubscribeToStream;
using IdentifyClient = EventStore.Client.Messages.IdentifyClient;
using NotHandled = EventStore.Client.Messages.NotHandled;
using PersistentSubscriptionAckEvents = EventStore.Client.Messages.PersistentSubscriptionAckEvents;
using PersistentSubscriptionConfirmation = EventStore.Client.Messages.PersistentSubscriptionConfirmation;
using PersistentSubscriptionNakEvents = EventStore.Client.Messages.PersistentSubscriptionNakEvents;
using PersistentSubscriptionStreamEventAppeared = EventStore.Client.Messages.PersistentSubscriptionStreamEventAppeared;
using ReadEvent = EventStore.Client.Messages.ReadEvent;
using ReadEventCompleted = EventStore.Client.Messages.ReadEventCompleted;
using ResolvedEvent = EventStore.Core.Data.ResolvedEvent;
using StreamEventAppeared = EventStore.Client.Messages.StreamEventAppeared;
using SubscribeToStream = EventStore.Client.Messages.SubscribeToStream;
using SubscriptionConfirmation = EventStore.Client.Messages.SubscriptionConfirmation;
using SubscriptionDropped = EventStore.Client.Messages.SubscriptionDropped;
using UnsubscribeFromStream = EventStore.Client.Messages.UnsubscribeFromStream;

namespace EventStore.Core.Services.Transport.Tcp;

public class ClientTcpDispatcher : ClientWriteTcpDispatcher {
	public ClientTcpDispatcher(int writeTimeoutMs) : this(TimeSpan.FromMilliseconds(writeTimeoutMs)) {
	}

	public ClientTcpDispatcher(TimeSpan writeTimeout) : base(writeTimeout) {
		AddUnwrapper(TcpCommand.Ping, UnwrapPing, ClientVersion.V2);
		AddWrapper<TcpMessage.PongMessage>(WrapPong, ClientVersion.V2);

		AddUnwrapper(TcpCommand.IdentifyClient, UnwrapIdentifyClient, ClientVersion.V2);

		AddUnwrapper(TcpCommand.ReadEvent, UnwrapReadEvent, ClientVersion.V2);
		AddWrapper<ClientMessage.ReadEventCompleted>(WrapReadEventCompleted, ClientVersion.V2);

		AddUnwrapper(TcpCommand.ReadStreamEventsForward, UnwrapReadStreamEventsForward, ClientVersion.V2);
		AddWrapper<ReadStreamEventsForwardCompleted>(WrapReadStreamEventsForwardCompleted,
			ClientVersion.V2);
		AddUnwrapper(TcpCommand.ReadStreamEventsBackward, UnwrapReadStreamEventsBackward, ClientVersion.V2);
		AddWrapper<ReadStreamEventsBackwardCompleted>(WrapReadStreamEventsBackwardCompleted,
			ClientVersion.V2);

		AddUnwrapper(TcpCommand.ReadAllEventsForward, UnwrapReadAllEventsForward, ClientVersion.V2);
		AddWrapper<ReadAllEventsForwardCompleted>(WrapReadAllEventsForwardCompleted,
			ClientVersion.V2);
		AddUnwrapper(TcpCommand.ReadAllEventsBackward, UnwrapReadAllEventsBackward, ClientVersion.V2);
		AddWrapper<ReadAllEventsBackwardCompleted>(WrapReadAllEventsBackwardCompleted,
			ClientVersion.V2);

		AddUnwrapper(TcpCommand.FilteredReadAllEventsForward, UnwrapFilteredReadAllEventsForward, ClientVersion.V2);
		AddWrapper<FilteredReadAllEventsForwardCompleted>(WrapFilteredReadAllEventsForwardCompleted,
			ClientVersion.V2);

		AddUnwrapper(TcpCommand.FilteredReadAllEventsBackward, UnwrapFilteredReadAllEventsBackward,
			ClientVersion.V2);
		AddWrapper<FilteredReadAllEventsBackwardCompleted>(WrapFilteredReadAllEventsBackwardCompleted,
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
		AddWrapper<CreatePersistentSubscriptionToStreamCompleted>(WrapCreatePersistentSubscriptionCompleted,
			ClientVersion.V2);
		AddWrapper<DeletePersistentSubscriptionToStreamCompleted>(WrapDeletePersistentSubscriptionCompleted,
			ClientVersion.V2);
		AddUnwrapper(TcpCommand.UpdatePersistentSubscription, UnwrapUpdatePersistentSubscription, ClientVersion.V2);
		AddWrapper<UpdatePersistentSubscriptionToStreamCompleted>(WrapUpdatePersistentSubscriptionCompleted,
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
		AddWrapper<ScavengeDatabaseInProgressResponse>(WrapScavengeDatabaseResponse, ClientVersion.V2);
		AddWrapper<ScavengeDatabaseStartedResponse>(WrapScavengeDatabaseResponse, ClientVersion.V2);
		AddWrapper<ScavengeDatabaseUnauthorizedResponse>(WrapScavengeDatabaseResponse, ClientVersion.V2);

		AddWrapper<ClientMessage.NotHandled>(WrapNotHandled, ClientVersion.V2);
		AddUnwrapper(TcpCommand.NotHandled, UnwrapNotHandled, ClientVersion.V2);

		AddWrapper<TcpMessage.NotAuthenticated>(WrapNotAuthenticated, ClientVersion.V2);
		AddWrapper<TcpMessage.Authenticated>(WrapAuthenticated, ClientVersion.V2);
	}


	private TcpPackage WrapCheckpointReached(ClientMessage.CheckpointReached msg) {
		var dto = new CheckpointReached(msg.Position.Value.CommitPosition,
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
		var dto = package.Data.Deserialize<IdentifyClient>();
		if (dto == null) return null;

		return new ClientMessage.IdentifyClient(package.CorrelationId, dto.Version, dto.ConnectionName);
	}

	private static TcpPackage WrapPong(TcpMessage.PongMessage message) {
		return new TcpPackage(TcpCommand.Pong, message.CorrelationId, message.Payload);
	}

	private static ClientMessage.ReadEvent
		UnwrapReadEvent(TcpPackage package, IEnvelope envelope, ClaimsPrincipal user) {
		var dto = package.Data.Deserialize<ReadEvent>();
		if (dto == null) return null;
		return new ClientMessage.ReadEvent(Guid.NewGuid(), package.CorrelationId, envelope, dto.EventStreamId,
			dto.EventNumber, dto.ResolveLinkTos, dto.RequireLeader, user);
	}

	private static TcpPackage WrapReadEventCompleted(ClientMessage.ReadEventCompleted msg) {
		var dto = new ReadEventCompleted(
			(ReadEventCompleted.Types.ReadEventResult)msg.Result,
			new ResolvedIndexedEvent(msg.Record.Event, msg.Record.Link), msg.Error);
		return new TcpPackage(TcpCommand.ReadEventCompleted, msg.CorrelationId, dto.Serialize());
	}

	private static ReadStreamEventsForward UnwrapReadStreamEventsForward(TcpPackage package,
		IEnvelope envelope, ClaimsPrincipal user) {
		var dto = package.Data.Deserialize<ReadStreamEvents>();
		if (dto == null) return null;
		return new ReadStreamEventsForward(Guid.NewGuid(), package.CorrelationId, envelope,
			dto.EventStreamId, dto.FromEventNumber, dto.MaxCount,
			dto.ResolveLinkTos, dto.RequireLeader, null, user,
			replyOnExpired: false);
	}

	private static TcpPackage WrapReadStreamEventsForwardCompleted(
		ReadStreamEventsForwardCompleted msg) {
		var dto = new ReadStreamEventsCompleted(
			ConvertToResolvedIndexedEvents(msg.Events),
			(ReadStreamEventsCompleted.Types.ReadStreamResult)msg.Result,
			msg.NextEventNumber, msg.LastEventNumber, msg.IsEndOfStream, msg.TfLastCommitPosition, msg.Error);
		return new TcpPackage(TcpCommand.ReadStreamEventsForwardCompleted, msg.CorrelationId, dto.Serialize());
	}

	private static ReadStreamEventsBackward UnwrapReadStreamEventsBackward(TcpPackage package,
		IEnvelope envelope, ClaimsPrincipal user) {
		var dto = package.Data.Deserialize<ReadStreamEvents>();
		if (dto == null) return null;
		return new ReadStreamEventsBackward(Guid.NewGuid(), package.CorrelationId, envelope,
			dto.EventStreamId, dto.FromEventNumber, dto.MaxCount,
			dto.ResolveLinkTos, dto.RequireLeader, null, user);
	}

	private static TcpPackage WrapReadStreamEventsBackwardCompleted(
		ReadStreamEventsBackwardCompleted msg) {
		var dto = new ReadStreamEventsCompleted(
			ConvertToResolvedIndexedEvents(msg.Events),
			(ReadStreamEventsCompleted.Types.ReadStreamResult)msg.Result,
			msg.NextEventNumber, msg.LastEventNumber, msg.IsEndOfStream, msg.TfLastCommitPosition, msg.Error);
		return new TcpPackage(TcpCommand.ReadStreamEventsBackwardCompleted, msg.CorrelationId, dto.Serialize());
	}

	private static ResolvedIndexedEvent[]
		ConvertToResolvedIndexedEvents(IReadOnlyList<ResolvedEvent> events) {
		var result = new ResolvedIndexedEvent[events.Count];
		for (int i = 0; i < events.Count; ++i) {
			result[i] = new ResolvedIndexedEvent(events[i].Event, events[i].Link);
		}

		return result;
	}

	private static ReadAllEventsForward UnwrapReadAllEventsForward(TcpPackage package,
		IEnvelope envelope, ClaimsPrincipal user) {
		var dto = package.Data.Deserialize<ReadAllEvents>();
		if (dto == null) return null;

		return new ReadAllEventsForward(Guid.NewGuid(), package.CorrelationId, envelope,
			dto.CommitPosition, dto.PreparePosition, dto.MaxCount,
			dto.ResolveLinkTos, dto.RequireLeader, null, user,
			replyOnExpired: false,
			longPollTimeout: null);
	}


	private static TcpPackage WrapReadAllEventsForwardCompleted(ReadAllEventsForwardCompleted msg) {
		var dto = new ReadAllEventsCompleted(
			msg.CurrentPos.CommitPosition, msg.CurrentPos.PreparePosition, ConvertToResolvedEvents(msg.Events),
			msg.NextPos.CommitPosition, msg.NextPos.PreparePosition,
			(ReadAllEventsCompleted.Types.ReadAllResult)msg.Result, msg.Error);
		return new TcpPackage(TcpCommand.ReadAllEventsForwardCompleted, msg.CorrelationId, dto.Serialize());
	}

	private static ReadAllEventsBackward UnwrapReadAllEventsBackward(TcpPackage package,
		IEnvelope envelope, ClaimsPrincipal user) {
		var dto = package.Data.Deserialize<ReadAllEvents>();
		if (dto == null) return null;
		return new ReadAllEventsBackward(Guid.NewGuid(), package.CorrelationId, envelope,
			dto.CommitPosition, dto.PreparePosition, dto.MaxCount,
			dto.ResolveLinkTos, dto.RequireLeader, null, user);
	}

	private static TcpPackage WrapReadAllEventsBackwardCompleted(ReadAllEventsBackwardCompleted msg) {
		var dto = new ReadAllEventsCompleted(
			msg.CurrentPos.CommitPosition, msg.CurrentPos.PreparePosition, ConvertToResolvedEvents(msg.Events),
			msg.NextPos.CommitPosition, msg.NextPos.PreparePosition,
			(ReadAllEventsCompleted.Types.ReadAllResult)msg.Result, msg.Error);
		return new TcpPackage(TcpCommand.ReadAllEventsBackwardCompleted, msg.CorrelationId, dto.Serialize());
	}

	private static FilteredReadAllEventsForward UnwrapFilteredReadAllEventsForward(TcpPackage package,
		IEnvelope envelope, ClaimsPrincipal user) {
		var dto = package.Data.Deserialize<FilteredReadAllEvents>();
		if (dto == null) return null;

		IEventFilter eventFilter = EventFilter.Get(true, dto.Filter);

		int maxSearchWindow = dto.MaxCount;
		if (dto.MaxSearchWindow > 0) {
			maxSearchWindow = dto.MaxSearchWindow;
		}

		return new FilteredReadAllEventsForward(Guid.NewGuid(), package.CorrelationId, envelope,
			dto.CommitPosition, dto.PreparePosition, dto.MaxCount,
			dto.ResolveLinkTos, dto.RequireLeader, maxSearchWindow, null, eventFilter, user,
			replyOnExpired: false,
			longPollTimeout: null);
	}

	private static TcpPackage WrapFilteredReadAllEventsForwardCompleted(
		FilteredReadAllEventsForwardCompleted msg) {
		var dto = new FilteredReadAllEventsCompleted(
			msg.CurrentPos.CommitPosition, msg.CurrentPos.PreparePosition, ConvertToResolvedEvents(msg.Events),
			msg.NextPos.CommitPosition, msg.NextPos.PreparePosition, msg.IsEndOfStream,
			(FilteredReadAllEventsCompleted.Types.FilteredReadAllResult)msg.Result, msg.Error);
		return new TcpPackage(TcpCommand.FilteredReadAllEventsForwardCompleted, msg.CorrelationId, dto.Serialize());
	}

	private static Client.Messages.ResolvedEvent[] ConvertToResolvedEvents(IReadOnlyList<ResolvedEvent> events) {
		var result = new Client.Messages.ResolvedEvent[events.Count];
		for (int i = 0; i < events.Count; ++i) {
			result[i] = new Client.Messages.ResolvedEvent(events[i]);
		}

		return result;
	}

	private static FilteredReadAllEventsBackward UnwrapFilteredReadAllEventsBackward(
		TcpPackage package,
		IEnvelope envelope, ClaimsPrincipal user) {
		var dto = package.Data.Deserialize<FilteredReadAllEvents>();
		if (dto == null) return null;

		IEventFilter eventFilter = EventFilter.Get(true, dto.Filter);

		int maxSearchWindow = dto.MaxCount;
		if (dto.MaxSearchWindow > 0) {
			maxSearchWindow = dto.MaxSearchWindow;
		}

		return new FilteredReadAllEventsBackward(Guid.NewGuid(), package.CorrelationId, envelope,
			dto.CommitPosition, dto.PreparePosition, dto.MaxCount,
			dto.ResolveLinkTos, dto.RequireLeader, maxSearchWindow, null, eventFilter, user, null);
	}

	private static TcpPackage WrapFilteredReadAllEventsBackwardCompleted(
		FilteredReadAllEventsBackwardCompleted msg) {
		var dto = new FilteredReadAllEventsCompleted(
			msg.CurrentPos.CommitPosition, msg.CurrentPos.PreparePosition, ConvertToResolvedEvents(msg.Events),
			msg.NextPos.CommitPosition, msg.NextPos.PreparePosition, msg.IsEndOfStream,
			(FilteredReadAllEventsCompleted.Types.FilteredReadAllResult)msg.Result, msg.Error);
		return new TcpPackage(TcpCommand.FilteredReadAllEventsBackwardCompleted, msg.CorrelationId,
			dto.Serialize());
	}

	private ClientMessage.SubscribeToStream UnwrapSubscribeToStream(TcpPackage package,
		IEnvelope envelope,
		ClaimsPrincipal user,
		TcpConnectionManager connection) {
		var dto = package.Data.Deserialize<SubscribeToStream>();
		if (dto == null) return null;
		return new ClientMessage.SubscribeToStream(Guid.NewGuid(), package.CorrelationId, envelope,
			connection.ConnectionId, dto.EventStreamId, dto.ResolveLinkTos, user);
	}

	private ClientMessage.FilteredSubscribeToStream UnwrapFilteredSubscribeToStream(TcpPackage package,
		IEnvelope envelope,
		ClaimsPrincipal user,
		TcpConnectionManager connection) {
		var dto = package.Data.Deserialize<FilteredSubscribeToStream>();
		if (dto == null) return null;

		IEventFilter eventFilter = EventFilter.Get(dto.EventStreamId.IsEmptyString(), dto.Filter);

		return new ClientMessage.FilteredSubscribeToStream(Guid.NewGuid(), package.CorrelationId, envelope,
			connection.ConnectionId, dto.EventStreamId, dto.ResolveLinkTos, user, eventFilter,
			dto.CheckpointInterval, checkpointIntervalCurrent: 0);
	}

	private ClientMessage.UnsubscribeFromStream UnwrapUnsubscribeFromStream(TcpPackage package, IEnvelope envelope,
		ClaimsPrincipal user) {
		var dto = package.Data.Deserialize<UnsubscribeFromStream>();
		if (dto == null) return null;
		return new ClientMessage.UnsubscribeFromStream(Guid.NewGuid(), package.CorrelationId, envelope, user);
	}

	private TcpPackage WrapSubscribedToStream(ClientMessage.SubscriptionConfirmation msg) {
		var dto = new SubscriptionConfirmation(msg.LastIndexedPosition, msg.LastEventNumber ?? 0);
		return new TcpPackage(TcpCommand.SubscriptionConfirmation, msg.CorrelationId, dto.Serialize());
	}

	private CreatePersistentSubscriptionToStream UnwrapCreatePersistentSubscription(
		TcpPackage package, IEnvelope envelope, ClaimsPrincipal user, TcpConnectionManager connection) {
		var dto = package.Data.Deserialize<CreatePersistentSubscription>();
		if (dto == null) return null;

		var namedConsumerStrategy = dto.NamedConsumerStrategy;
		if (string.IsNullOrEmpty(namedConsumerStrategy)) {
			namedConsumerStrategy = dto.PreferRoundRobin
				? SystemConsumerStrategies.RoundRobin
				: SystemConsumerStrategies.DispatchToSingle;
		}

		return new CreatePersistentSubscriptionToStream(Guid.NewGuid(), package.CorrelationId, envelope,
			dto.EventStreamId, dto.SubscriptionGroupName, dto.ResolveLinkTos, dto.StartFrom,
			dto.MessageTimeoutMilliseconds,
			dto.RecordStatistics, dto.MaxRetryCount, dto.BufferSize, dto.LiveBufferSize,
			dto.ReadBatchSize, dto.CheckpointAfterTime, dto.CheckpointMinCount,
			dto.CheckpointMaxCount, dto.SubscriberMaxCount, namedConsumerStrategy,
			user);
	}

	private UpdatePersistentSubscriptionToStream UnwrapUpdatePersistentSubscription(
		TcpPackage package, IEnvelope envelope, ClaimsPrincipal user, TcpConnectionManager connection) {
		var dto = package.Data.Deserialize<UpdatePersistentSubscription>();
		if (dto == null) return null;

		var namedConsumerStrategy = dto.NamedConsumerStrategy;
		if (string.IsNullOrEmpty(namedConsumerStrategy)) {
			namedConsumerStrategy = dto.PreferRoundRobin
				? SystemConsumerStrategies.RoundRobin
				: SystemConsumerStrategies.DispatchToSingle;
		}

		return new UpdatePersistentSubscriptionToStream(Guid.NewGuid(), package.CorrelationId, envelope,
			dto.EventStreamId, dto.SubscriptionGroupName, dto.ResolveLinkTos, dto.StartFrom,
			dto.MessageTimeoutMilliseconds,
			dto.RecordStatistics, dto.MaxRetryCount, dto.BufferSize, dto.LiveBufferSize,
			dto.ReadBatchSize, dto.CheckpointAfterTime, dto.CheckpointMinCount,
			dto.CheckpointMaxCount, dto.SubscriberMaxCount, namedConsumerStrategy,
			user);
	}

	private DeletePersistentSubscriptionToStream UnwrapDeletePersistentSubscription(
		TcpPackage package, IEnvelope envelope, ClaimsPrincipal user, TcpConnectionManager connection) {
		var dto = package.Data.Deserialize<CreatePersistentSubscription>();
		if (dto == null) return null;
		return new DeletePersistentSubscriptionToStream(Guid.NewGuid(), package.CorrelationId, envelope,
			dto.EventStreamId, dto.SubscriptionGroupName, user);
	}

	private TcpPackage WrapDeletePersistentSubscriptionCompleted(
		DeletePersistentSubscriptionToStreamCompleted msg) {
		var dto = new DeletePersistentSubscriptionCompleted(
			(DeletePersistentSubscriptionCompleted.Types.DeletePersistentSubscriptionResult)msg
				.Result, msg.Reason);
		return new TcpPackage(TcpCommand.DeletePersistentSubscriptionCompleted, msg.CorrelationId, dto.Serialize());
	}

	private TcpPackage WrapCreatePersistentSubscriptionCompleted(
		CreatePersistentSubscriptionToStreamCompleted msg) {
		var dto = new CreatePersistentSubscriptionCompleted(
			(CreatePersistentSubscriptionCompleted.Types.CreatePersistentSubscriptionResult)msg
				.Result, msg.Reason);
		return new TcpPackage(TcpCommand.CreatePersistentSubscriptionCompleted, msg.CorrelationId, dto.Serialize());
	}

	private TcpPackage WrapUpdatePersistentSubscriptionCompleted(
		UpdatePersistentSubscriptionToStreamCompleted msg) {
		var dto = new UpdatePersistentSubscriptionCompleted(
			(UpdatePersistentSubscriptionCompleted.Types.UpdatePersistentSubscriptionResult)msg
				.Result, msg.Reason);
		return new TcpPackage(TcpCommand.UpdatePersistentSubscriptionCompleted, msg.CorrelationId, dto.Serialize());
	}


	private ConnectToPersistentSubscriptionToStream UnwrapConnectToPersistentSubscription(
		TcpPackage package, IEnvelope envelope, ClaimsPrincipal user, TcpConnectionManager connection) {
		var dto = package.Data.Deserialize<ConnectToPersistentSubscription>();
		if (dto == null) return null;
		return new ConnectToPersistentSubscriptionToStream(Guid.NewGuid(), package.CorrelationId, envelope,
			connection.ConnectionId, connection.ClientConnectionName, dto.SubscriptionId, dto.EventStreamId, dto.AllowedInFlightMessages,
			connection.RemoteEndPoint.ToString(), user);
	}

	private ClientMessage.PersistentSubscriptionAckEvents UnwrapPersistentSubscriptionAckEvents(
		TcpPackage package, IEnvelope envelope, ClaimsPrincipal user, TcpConnectionManager connection) {
		var dto = package.Data.Deserialize<PersistentSubscriptionAckEvents>();
		if (dto == null) return null;
		return new ClientMessage.PersistentSubscriptionAckEvents(
			Guid.NewGuid(), package.CorrelationId, envelope, dto.SubscriptionId,
			dto.ProcessedEventIds.Select(x => new Guid(x.ToByteArray())).ToArray(), user);
	}

	private PersistentSubscriptionNackEvents UnwrapPersistentSubscriptionNackEvents(
		TcpPackage package, IEnvelope envelope, ClaimsPrincipal user, TcpConnectionManager connection) {
		var dto = package.Data.Deserialize<PersistentSubscriptionNakEvents>();
		if (dto == null) return null;
		return new PersistentSubscriptionNackEvents(
			Guid.NewGuid(), package.CorrelationId, envelope, dto.SubscriptionId,
			dto.Message, (PersistentSubscriptionNackEvents.NakAction)dto.Action,
			dto.ProcessedEventIds.Select(x => new Guid(x.ToByteArray())).ToArray(), user);
	}

	private TcpPackage
		WrapPersistentSubscriptionConfirmation(ClientMessage.PersistentSubscriptionConfirmation msg) {
		var dto = new PersistentSubscriptionConfirmation(msg.LastIndexedPosition,
			msg.SubscriptionId, msg.LastEventNumber ?? 0);
		return new TcpPackage(TcpCommand.PersistentSubscriptionConfirmation, msg.CorrelationId, dto.Serialize());
	}

	private static TcpPackage WrapPersistentSubscriptionStreamEventAppeared(
		ClientMessage.PersistentSubscriptionStreamEventAppeared msg) {
		var dto = new PersistentSubscriptionStreamEventAppeared(
			new ResolvedIndexedEvent(msg.Event.Event, msg.Event.Link), msg.RetryCount);
		return new TcpPackage(TcpCommand.PersistentSubscriptionStreamEventAppeared, msg.CorrelationId,
			dto.Serialize());
	}

	private TcpPackage WrapStreamEventAppeared(ClientMessage.StreamEventAppeared msg) {
		var dto = new StreamEventAppeared(new Client.Messages.ResolvedEvent(msg.Event));
		return new TcpPackage(TcpCommand.StreamEventAppeared, msg.CorrelationId, dto.Serialize());
	}

	private TcpPackage WrapSubscriptionDropped(ClientMessage.SubscriptionDropped msg) {
		var dto = new SubscriptionDropped(
			(SubscriptionDropped.Types.SubscriptionDropReason)msg.Reason);
		return new TcpPackage(TcpCommand.SubscriptionDropped, msg.CorrelationId, dto.Serialize());
	}

	private ClientMessage.ScavengeDatabase UnwrapScavengeDatabase(TcpPackage package, IEnvelope envelope,
		ClaimsPrincipal user) {
		return new ClientMessage.ScavengeDatabase(envelope, package.CorrelationId, user, 0, 1, null, null, false);
	}

	private TcpPackage WrapScavengeDatabaseResponse(Message msg) {
		ScavengeDatabaseResponse.Types.ScavengeResult result;
		string scavengeId;
		Guid correlationId;

		switch (msg) {
			case ScavengeDatabaseStartedResponse startedResponse:
				result = ScavengeDatabaseResponse.Types.ScavengeResult.Started;
				scavengeId = startedResponse.ScavengeId;
				correlationId = startedResponse.CorrelationId;
				break;
			case ScavengeDatabaseInProgressResponse inProgressResponse:
				result = ScavengeDatabaseResponse.Types.ScavengeResult.InProgress;
				scavengeId = inProgressResponse.ScavengeId;
				correlationId = inProgressResponse.CorrelationId;
				break;
			case ScavengeDatabaseUnauthorizedResponse unauthorizedResponse:
				result = ScavengeDatabaseResponse.Types.ScavengeResult.Unauthorized;
				scavengeId = unauthorizedResponse.ScavengeId;
				correlationId = unauthorizedResponse.CorrelationId;
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}

		var dto = new ScavengeDatabaseResponse(result, scavengeId);
		return new TcpPackage(TcpCommand.ScavengeDatabaseResponse, correlationId, dto.Serialize());
	}

	private ClientMessage.NotHandled UnwrapNotHandled(TcpPackage package, IEnvelope envelope) {
		var dto = package.Data.Deserialize<NotHandled>();
		if (dto == null) return null;
		var reason = dto.Reason switch {
			NotHandled.Types.NotHandledReason.NotReady => ClientMessage.NotHandled.Types.NotHandledReason.NotReady,
			NotHandled.Types.NotHandledReason.TooBusy => ClientMessage.NotHandled.Types.NotHandledReason.TooBusy,
			NotHandled.Types.NotHandledReason.NotLeader => ClientMessage.NotHandled.Types.NotHandledReason.NotLeader,
			NotHandled.Types.NotHandledReason.IsReadOnly => ClientMessage.NotHandled.Types.NotHandledReason.IsReadOnly,
			_ => throw new ArgumentOutOfRangeException()
		};
		var leaderInfoDto = dto.AdditionalInfo switch {
			{} ai => ai.ToByteArray().Deserialize<NotHandled.Types.LeaderInfo>(),
			_ => null
		};

		var leaderInfo = leaderInfoDto switch {
			{ ExternalTcpAddress: { } } => new ClientMessage.NotHandled.Types.LeaderInfo(
				new DnsEndPoint(leaderInfoDto.ExternalTcpAddress, leaderInfoDto.ExternalTcpPort), false, new DnsEndPoint(leaderInfoDto.HttpAddress, leaderInfoDto.HttpPort)),
			{ ExternalSecureTcpAddress: { } } => new ClientMessage.NotHandled.Types.LeaderInfo(
				new DnsEndPoint(leaderInfoDto.ExternalSecureTcpAddress, leaderInfoDto.ExternalSecureTcpPort), true, new DnsEndPoint(leaderInfoDto.HttpAddress, leaderInfoDto.HttpPort)),
			_ => null
		};
		return new ClientMessage.NotHandled(package.CorrelationId, reason, leaderInfo);
	}

	private TcpPackage WrapNotHandled(ClientMessage.NotHandled msg) {
		var dto = new Client.Messages.NotHandled(msg);
		return new TcpPackage(TcpCommand.NotHandled, msg.CorrelationId, dto.Serialize());
	}

	private TcpPackage WrapNotAuthenticated(TcpMessage.NotAuthenticated msg) {
		return new TcpPackage(TcpCommand.NotAuthenticated, msg.CorrelationId,
			Helper.UTF8NoBom.GetBytes(msg.Reason ?? string.Empty));
	}

	private TcpPackage WrapAuthenticated(TcpMessage.Authenticated msg) {
		return new TcpPackage(TcpCommand.Authenticated, msg.CorrelationId, Empty.ByteArray);
	}
}
