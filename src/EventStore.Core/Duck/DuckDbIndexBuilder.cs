// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Duck.Default;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLog.Chunks;
using Eventuous.Subscriptions.Checkpoints;
using Serilog;
using static EventStore.Core.Messages.SystemMessage;

namespace EventStore.Core.Duck;

class DuckDbIndexBuilder(TFChunkDbConfig dbConfig, IPublisher publisher) : IAsyncHandle<SystemReady>, IAsyncHandle<BecomeShuttingDown> {
	static readonly ILogger Log = Serilog.Log.Logger.ForContext<DuckDbIndexBuilder>();
	InternalSubscription _subscription;
	IndexCheckpointStore _checkpointStore;
	DefaultIndexHandler _handler;

	public async ValueTask HandleAsync(SystemReady message, CancellationToken token) {
		DuckDb.InitDb(dbConfig);
		_handler = new();
		DuckDb.InitIndexes(_handler);
		_checkpointStore = new(_handler);
		_subscription = new(publisher, _checkpointStore, _handler);
		await _subscription.Subscribe(
			id => Log.Information("Index subscription {Subscription} subscribed", id),
			(id, reason, ex) => Log.Warning(ex, "Index subscription {Subscription} dropped {Reason}", id, reason),
			token
		);
	}

	public async ValueTask HandleAsync(BecomeShuttingDown message, CancellationToken token) {
		_handler.Commit();
		await _subscription.Unsubscribe(id => Log.Information("Index subscription {Subscription} unsubscribed", id), token);
	}

	public DefaultIndexHandler GetHandler() => _handler;
}
