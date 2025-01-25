// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Services.TimerService;
using System.Diagnostics;
using EventStore.Core.Data;

namespace EventStore.Core.Services.RequestManager;

public class RequestManagementService(IPublisher bus, TimeSpan prepareTimeout, TimeSpan commitTimeout, bool explicitTransactionsSupported) :
		IHandle<SystemMessage.SystemInit>,
		IHandle<ClientMessage.WriteEvents>,
		IHandle<ClientMessage.DeleteStream>,
		IHandle<ClientMessage.TransactionStart>,
		IHandle<ClientMessage.TransactionWrite>,
		IHandle<ClientMessage.TransactionCommit>,
		IHandle<StorageMessage.RequestCompleted>,
		IHandle<StorageMessage.AlreadyCommitted>,
		IHandle<StorageMessage.PrepareAck>,
		IHandle<ReplicationTrackingMessage.ReplicatedTo>,
		IHandle<ReplicationTrackingMessage.IndexedTo>,
		IHandle<StorageMessage.CommitIndexed>,
		IHandle<StorageMessage.WrongExpectedVersion>,
		IHandle<StorageMessage.InvalidTransaction>,
		IHandle<StorageMessage.StreamDeleted>,
		IHandle<StorageMessage.RequestManagerTimerTick>,
		IHandle<SystemMessage.StateChangeMessage> {
	private readonly IPublisher _bus = Ensure.NotNull(bus);
	private readonly TimerMessage.Schedule _tickRequestMessage = TimerMessage.Schedule.Create(TimeSpan.FromMilliseconds(1000), bus, new StorageMessage.RequestManagerTimerTick());
	private readonly Dictionary<Guid, RequestManagerBase> _currentRequests = new();
	private readonly Dictionary<Guid, Stopwatch> _currentTimedRequests = new();
	private readonly CommitSource _commitSource = new();
	private VNodeState _nodeState;

	public void Handle(ClientMessage.WriteEvents message) {
		var manager = new WriteEvents(
							_bus,
							commitTimeout,
							message.Envelope,
							message.InternalCorrId,
							message.CorrelationId,
							message.EventStreamId,
							message.ExpectedVersion,
							message.Events,
							_commitSource,
							message.CancellationToken);
		_currentRequests.Add(message.InternalCorrId, manager);
		_currentTimedRequests.Add(message.InternalCorrId, Stopwatch.StartNew());
		manager.Start();
	}

	public void Handle(ClientMessage.DeleteStream message) {
		var manager = new DeleteStream(
							_bus,
							commitTimeout,
							message.Envelope,
							message.InternalCorrId,
							message.CorrelationId,
							message.EventStreamId,
							message.ExpectedVersion,
							message.HardDelete,
							_commitSource,
							message.CancellationToken);
		_currentRequests.Add(message.InternalCorrId, manager);
		_currentTimedRequests.Add(message.InternalCorrId, Stopwatch.StartNew());
		manager.Start();
	}

	public void Handle(ClientMessage.TransactionStart message) {
		if (!explicitTransactionsSupported) {
			var reply = new ClientMessage.TransactionStartCompleted(
				message.CorrelationId,
				default,
				OperationResult.InvalidTransaction,
				"Explicit transactions are not supported");
			message.Envelope.ReplyWith(reply);
			return;
		}

		var manager = new TransactionStart(
							_bus,
							prepareTimeout,
							message.Envelope,
							message.InternalCorrId,
							message.CorrelationId,
							message.EventStreamId,
							message.ExpectedVersion,
							_commitSource);
		_currentRequests.Add(message.InternalCorrId, manager);
		_currentTimedRequests.Add(message.InternalCorrId, Stopwatch.StartNew());
		manager.Start();
	}

	public void Handle(ClientMessage.TransactionWrite message) {
		if (!explicitTransactionsSupported) {
			var reply = new ClientMessage.TransactionWriteCompleted(
				message.CorrelationId,
				default,
				OperationResult.InvalidTransaction,
				"Explicit transactions are not supported");
			message.Envelope.ReplyWith(reply);
			return;
		}

		var manager = new TransactionWrite(
							_bus,
							prepareTimeout,
							message.Envelope,
							message.InternalCorrId,
							message.CorrelationId,
							message.Events,
							message.TransactionId,
							_commitSource);
		_currentRequests.Add(message.InternalCorrId, manager);
		_currentTimedRequests.Add(message.InternalCorrId, Stopwatch.StartNew());
		manager.Start();
	}

	public void Handle(ClientMessage.TransactionCommit message) {
		if (!explicitTransactionsSupported) {
			var reply = new ClientMessage.TransactionCommitCompleted(
				message.CorrelationId,
				default,
				OperationResult.InvalidTransaction,
				"Explicit transactions are not supported");
			message.Envelope.ReplyWith(reply);
			return;
		}

		var manager = new TransactionCommit(
							_bus,
							prepareTimeout,
							commitTimeout,
							message.Envelope,
							message.InternalCorrId,
							message.CorrelationId,
							message.TransactionId,
							_commitSource);
		_currentRequests.Add(message.InternalCorrId, manager);
		_currentTimedRequests.Add(message.InternalCorrId, Stopwatch.StartNew());
		manager.Start();
	}


	public void Handle(SystemMessage.StateChangeMessage message) {

		if (_nodeState == VNodeState.Leader && message.State is not VNodeState.Leader or VNodeState.ResigningLeader) {
			var keys = _currentRequests.Keys;
			foreach (var key in keys) {
				if (_currentRequests.Remove(key, out var manager)) {
					manager.Dispose();
				}
			}
		}
		_nodeState = message.State;
	}

	public void Handle(SystemMessage.SystemInit message) {
		_bus.Publish(_tickRequestMessage);
	}

	public void Handle(StorageMessage.RequestManagerTimerTick message) {
		foreach (var currentRequest in _currentRequests) {
			currentRequest.Value.Handle(message);
		}
		//TODO(clc): if we have become resigning leader should all requests be actively disposed?
		if (_nodeState == VNodeState.ResigningLeader && _currentRequests.Count == 0) {
			_bus.Publish(new SystemMessage.RequestQueueDrained());
		}

		_bus.Publish(_tickRequestMessage);
	}

	public void Handle(StorageMessage.RequestCompleted message) {
		if (_currentTimedRequests.TryGetValue(message.CorrelationId, out _)) {
			// todo: histogram metric?
			_currentTimedRequests.Remove(message.CorrelationId);
		}

		if (!_currentRequests.Remove(message.CorrelationId)) {
			// noop. RequestManager guarantees not complete twice now.
			// and we will legitimately get in here when StateChangeMessage removes
			// entries from _currentRequests
		}
	}

	public void Handle(ReplicationTrackingMessage.ReplicatedTo message) => _commitSource.Handle(message);
	public void Handle(ReplicationTrackingMessage.IndexedTo message) => _commitSource.Handle(message);

	public void Handle(StorageMessage.AlreadyCommitted message) => DispatchInternal(message.CorrelationId, message, static (manager, m) => manager.Handle(m));
	public void Handle(StorageMessage.PrepareAck message) => DispatchInternal(message.CorrelationId, message, static (manager, m) => manager.Handle(m));
	public void Handle(StorageMessage.CommitIndexed message) => DispatchInternal(message.CorrelationId, message, static (manager, m) => manager.Handle(m));
	public void Handle(StorageMessage.WrongExpectedVersion message) => DispatchInternal(message.CorrelationId, message, static (manager, m) => manager.Handle(m));
	public void Handle(StorageMessage.InvalidTransaction message) => DispatchInternal(message.CorrelationId, message, static (manager, m) => manager.Handle(m));
	public void Handle(StorageMessage.StreamDeleted message) => DispatchInternal(message.CorrelationId, message, static (manager, m) => manager.Handle(m));

	private void DispatchInternal<T>(Guid correlationId, T message, Action<RequestManagerBase, T> handle) where T : Message {
		if (_currentRequests.TryGetValue(correlationId, out var manager)) {
			handle(manager, message);
		}
	}
}
