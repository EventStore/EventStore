using System;
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
	public class ScavengeStateBuilder {
		private readonly ILongHasher<string> _hasher;
		private readonly IMetastreamLookup<string> _metastreamLookup;

		private Tracer _tracer;
		private ObjectPool<SqliteConnection> _connectionPool;
		private Type _cancelWhenCheckpointingType;
		private CancellationTokenSource _cancellationTokenSource;
		private Action<ScavengeState<string>> _mutateState;

		public ScavengeStateBuilder(
			ILongHasher<string> hasher,
			IMetastreamLookup<string> metastreamLookup) {

			_hasher = hasher;
			_metastreamLookup = metastreamLookup;
			_mutateState = x => { };
		}

		public ScavengeStateBuilder TransformBuilder(Func<ScavengeStateBuilder, ScavengeStateBuilder> f) =>
			f(this);

		public ScavengeStateBuilder CancelWhenCheckpointing(Type type, CancellationTokenSource cts) {
			_cancelWhenCheckpointingType = type;
			_cancellationTokenSource = cts;
			return this;
		}

		public ScavengeStateBuilder MutateState(Action<ScavengeState<string>> f) {
			var wrapped = _mutateState;
			_mutateState = state => {
				wrapped(state);
				f(state);
			};
			return this;
		}

		public ScavengeStateBuilder WithTracer(Tracer tracer) {
			_tracer = tracer;
			return this;
		}

		public ScavengeStateBuilder WithConnectionPool(ObjectPool<SqliteConnection> connectionPool) {
			_connectionPool = connectionPool;
			return this;
		}

		public ScavengeState<string> Build() {
			var state = BuildInternal();
			state.Init();
			_mutateState(state);
			return state;
		}

		private ScavengeState<string> BuildInternal() {
			if (_connectionPool == null)
				throw new Exception("call WithConnectionPool(...)");

			var map = new ConcurrentDictionary<IScavengeStateBackend<string>, SqliteConnection>();
			var backendPool = new ObjectPool<IScavengeStateBackend<string>>(
				objectPoolName: "scavenge backend pool",
				initialCount: 1,
				maxCount: TFChunkScavenger.MaxThreadCount + 1,
				factory: () => {
					var connection = _connectionPool.Get();
					var sqlite = new SqliteScavengeBackend<string>();
					sqlite.Initialize(connection);

					var backend = new AdHocScavengeBackendInterceptor<string>(sqlite);

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
							new TracingOriginalStreamScavengeMap<string>(backend.OriginalCollisionStorage, _tracer);
					}
					map[backend] = connection;
					return backend;
				},
				dispose: backend => _connectionPool.Return(map[backend]));

			var scavengeState = new ScavengeState<string>(
				_hasher,
				_metastreamLookup,
				backendPool,
				100_000);

			return scavengeState;
		}
	}
}
