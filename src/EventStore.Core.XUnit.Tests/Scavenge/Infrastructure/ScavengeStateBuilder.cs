﻿using System;
using System.Collections.Concurrent;
using System.Threading;
using EventStore.Core.DataStructures;
using EventStore.Core.Index.Hashes;
using EventStore.Core.LogAbstraction;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Scavenging;
using EventStore.Core.TransactionLog.Scavenging.Sqlite;
using Microsoft.Data.Sqlite;

namespace EventStore.Core.XUnit.Tests.Scavenge {
	public class ScavengeStateBuilder<TStreamId> {
		private readonly ILongHasher<TStreamId> _hasher;
		private readonly IMetastreamLookup<TStreamId> _metastreamLookup;

		private Tracer _tracer;
		private ObjectPool<SqliteConnection> _connectionPool;
		private Type _cancelWhenCheckpointingType;
		private CancellationTokenSource _cancellationTokenSource;
		private Action<ScavengeState<TStreamId>> _mutateState;

		public ScavengeStateBuilder(
			ILongHasher<TStreamId> hasher,
			IMetastreamLookup<TStreamId> metastreamLookup) {

			_hasher = hasher;
			_metastreamLookup = metastreamLookup;
			_mutateState = x => { };
		}

		public ScavengeStateBuilder<TStreamId> TransformBuilder(
			Func<ScavengeStateBuilder<TStreamId>, ScavengeStateBuilder<TStreamId>> f) =>
			f(this);

		public ScavengeStateBuilder<TStreamId> CancelWhenCheckpointing(Type type, CancellationTokenSource cts) {
			_cancelWhenCheckpointingType = type;
			_cancellationTokenSource = cts;
			return this;
		}

		public ScavengeStateBuilder<TStreamId> MutateState(Action<ScavengeState<TStreamId>> f) {
			var wrapped = _mutateState;
			_mutateState = state => {
				wrapped(state);
				f(state);
			};
			return this;
		}

		public ScavengeStateBuilder<TStreamId> WithTracer(Tracer tracer) {
			_tracer = tracer;
			return this;
		}

		public ScavengeStateBuilder<TStreamId> WithConnectionPool(ObjectPool<SqliteConnection> connectionPool) {
			_connectionPool = connectionPool;
			return this;
		}

		public ScavengeState<TStreamId> Build() {
			var state = BuildInternal();
			_mutateState(state);
			return state;
		}

		private ScavengeState<TStreamId> BuildInternal() {
			if (_connectionPool == null)
				throw new Exception("call WithConnectionPool(...)");

			var map = new ConcurrentDictionary<IScavengeStateBackend<TStreamId>, SqliteConnection>();
			var backendPool = new ObjectPool<IScavengeStateBackend<TStreamId>>(
				objectPoolName: "scavenge backend pool",
				initialCount: 1,
				maxCount: TFChunkScavenger.MaxThreadCount + 1,
				factory: () => {
					var connection = _connectionPool.Get();
					var sqlite = new SqliteScavengeBackend<TStreamId>();
					sqlite.Initialize(connection);

					var backend = new AdHocScavengeBackendInterceptor<TStreamId>(sqlite);

					var transactionFactory = sqlite.TransactionFactory;

					if (_tracer != null)
						transactionFactory = new TracingTransactionFactory<SqliteTransaction>(transactionFactory, _tracer);

					ITransactionManager transactionManager = new TransactionManager<SqliteTransaction>(
						transactionFactory,
						backend.CheckpointStorage);

					transactionManager = new AdHocTransactionManager(
						transactionManager,
						(continuation, checkpoint) => {
							if (checkpoint.GetType() == _cancelWhenCheckpointingType) {
								_cancellationTokenSource.Cancel();
							}
							continuation(checkpoint);
						});

					if (_tracer != null) {
						backend.TransactionManager = new TracingTransactionManager(transactionManager, _tracer);
						backend.OriginalStorage =
							new TracingOriginalStreamScavengeMap<ulong>(backend.OriginalStorage, _tracer);
						backend.OriginalCollisionStorage =
							new TracingOriginalStreamScavengeMap<TStreamId>(backend.OriginalCollisionStorage, _tracer);
					}
					map[backend] = connection;
					return backend;
				},
				dispose: backend => _connectionPool.Return(map[backend]));

			var scavengeState = new ScavengeState<TStreamId>(
				_hasher,
				_metastreamLookup,
				backendPool);

			return scavengeState;
		}
	}
}
