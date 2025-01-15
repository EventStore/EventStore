// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.Transport.Common;
using EventStore.Core.Services.Transport.Enumerators;
using EventStore.Core.Services.UserManagement;
using Eventuous;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Subscriptions.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog.Extensions.Logging;
using ILogger = Serilog.ILogger;

namespace EventStore.Core.Duck;

public class InternalSubscription(
	IPublisher publisher,
	ICheckpointStore checkpointStore,
	IEventHandler eventHandler)
	: EventSubscriptionWithCheckpoint<InternalSubscriptionOptions>(
		new() { SubscriptionId = "indexBuilder", ThrowOnError = true, CheckpointCommitBatchSize = 10000 },
		checkpointStore, new ConsumePipe().AddDefaultConsumer(eventHandler),
		1, SubscriptionKind.All, new SerilogLoggerFactory(),
		new EventSerializer(), null) {
	const uint DefaultCheckpointIntervalMultiplier = 1000;

	readonly CancellationTokenSource _cts = new();
	Enumerator.AllSubscriptionFiltered _sub;
	Task _runner;

	static readonly ILogger _log = Serilog.Log.ForContext<InternalSubscription>();

	ILoggerFactory _loggerFactory = new SerilogLoggerFactory(_log);

	protected override async ValueTask Subscribe(CancellationToken cancellationToken) {
		var (_, position) = await GetCheckpoint(cancellationToken);
		var startFrom = position == null ? Position.Start : Position.FromInt64((long)position, (long)position);
		_sub = new(
			bus: publisher,
			expiryStrategy: new DefaultExpiryStrategy(),
			checkpoint: startFrom,
			resolveLinks: false,
			eventFilter: EventFilter.DefaultAllFilter,
			user: SystemAccounts.System,
			requiresLeader: false,
			maxSearchWindow: null,
			checkpointIntervalMultiplier: DefaultCheckpointIntervalMultiplier,
			cancellationToken: cancellationToken
		);

		_runner = Task.Run(ProcessEvents, cancellationToken);
	}

	async Task ProcessEvents() {
		while (!_cts.IsCancellationRequested) {
			if (!await _sub.MoveNextAsync()) // not sure if we need to retry forever or if the enumerator will do that for us
				break;

			if (_sub.Current is not ReadResponse.EventReceived eventReceived) continue;

			try {
				// Logger.ConfigureIfNull(Options.SubscriptionId, _loggerFactory);
				var context = CreateContext(eventReceived.Event);
				await HandleInternal(context);
			} catch (Exception e) {
				_log.Error(e, "Error while processing event {EventType}", eventReceived.Event.Event.EventType);
				throw;
			}
		}
	}

	MessageConsumeContext CreateContext(ResolvedEvent re) {
		// var evt = DeserializeData(
		// 	string.Empty,
		// 	re.Event.EventType,
		// 	re.Event.Data,
		// 	re.Event.EventStreamId,
		// 	(ulong)re.Event.EventNumber
		// );

		return new(
			re.Event.EventId.ToString(),
			re.Event.EventType,
			string.Empty,
			re.Event.EventStreamId,
			(ulong)re.Event.EventNumber,
			(ulong)re.OriginalEventNumber,
			(ulong)re.EventPosition.Value.CommitPosition,
			Sequence++,
			re.Event.TimeStamp,
			new(),
			new(),
			SubscriptionId,
			_cts.Token
		);
	}

	protected override async ValueTask Unsubscribe(CancellationToken cancellationToken) {
		try {
			await _cts.CancelAsync();
			await _runner;
		} catch {
			//
		} finally {
			await _sub.DisposeAsync();
		}
	}

	class NoFilter : IEventFilter {
		public bool IsEventAllowed(EventRecord eventRecord) {
			return true;
		}
	}
}

public record InternalSubscriptionOptions : SubscriptionWithCheckpointOptions;

public class EventSerializer : IEventSerializer {
	public DeserializationResult DeserializeEvent(ReadOnlySpan<byte> data, string eventType, string contentType)
		=> new DeserializationResult.SuccessfullyDeserialized(JsonNode.Parse(data)!);

	public SerializationResult SerializeEvent(object evt) => throw new NotImplementedException();
}
