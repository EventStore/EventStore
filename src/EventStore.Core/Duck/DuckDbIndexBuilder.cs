// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Duck.Default;
using EventStore.Core.TransactionLog.Chunks;
using Serilog;
using static EventStore.Core.Messages.SystemMessage;

namespace EventStore.Core.Duck;

class DuckDbIndexBuilder : IAsyncHandle<SystemReady>, IAsyncHandle<BecomeShuttingDown> {
	static readonly ILogger Log = Serilog.Log.Logger.ForContext<DuckDbIndexBuilder>();
	InternalSubscription _subscription;
	IndexCheckpointStore _checkpointStore;
	DefaultIndexHandler _handler;
	readonly DuckDb _db;

	internal DefaultIndex DefaultIndex;
	readonly IPublisher _publisher;

	public DuckDbIndexBuilder(TFChunkDbConfig dbConfig, IPublisher publisher) {
		_publisher = publisher;
		_db = new(dbConfig);
		_db.InitDb();
		DefaultIndex = new(_db);
	}

	public async ValueTask HandleAsync(SystemReady message, CancellationToken token) {
		_handler = new(_db, DefaultIndex);
		DefaultIndex.Init();
		_checkpointStore = new(DefaultIndex, _handler);
		_subscription = new(_publisher, _checkpointStore, _handler);
		await _subscription.Subscribe(
			id => Log.Information("Index subscription {Subscription} subscribed", id),
			(id, reason, ex) => Log.Warning(ex, "Index subscription {Subscription} dropped {Reason}", id, reason),
			token
		);
	}

	public async ValueTask HandleAsync(BecomeShuttingDown message, CancellationToken token) {
		_handler.Commit(false);
		await Task.Delay(100, token);
		_db.Close();
		// await _subscription.Unsubscribe(id => Log.Information("Index subscription {Subscription} unsubscribed", id), token);
	}
}
