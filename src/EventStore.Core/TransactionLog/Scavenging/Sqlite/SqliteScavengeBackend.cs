using System;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using Microsoft.Data.Sqlite;

namespace EventStore.Core.TransactionLog.Scavenging.Sqlite {
	public class SqliteScavengeBackend {
		protected static readonly ILogger Log = LogManager.GetLoggerFor<SqliteScavengeBackend>();
	}

	// Encapsulates a connection to sqlite, complete with prepared statements and an API to access it
	public class SqliteScavengeBackend<TStreamId> : SqliteScavengeBackend, IScavengeStateBackend<TStreamId> {
		// WAL with SYNCHRONOUS NORMAL means that
		//  - commiting a transaction does not wait to it to flush to disk
		//  - which is nice and quick, but means in powerloss the last x transactions
		//    can be lost. the database will be in a valid state.
		//  - this is suitable for us because scavenge will continue from the last
		//    persisted checkpoint.
		private const string SqliteWalJournalMode = "wal";
		private const int SqliteNormalSynchronousValue = 1;
		private const int DefaultSqliteCacheSize = 2 * 1024 * 1024;
		private const int SqlitePageSize = 16 * 1024;
		private readonly int _cacheSizeInBytes;
		private SqliteBackend _sqliteBackend;

		private const int SchemaVersion = 1;
		
		public IScavengeMap<TStreamId, Unit> CollisionStorage { get; private set; }
		public IScavengeMap<ulong,TStreamId> Hashes { get; private set; }
		public IMetastreamScavengeMap<ulong> MetaStorage { get; private set; }
		public IMetastreamScavengeMap<TStreamId> MetaCollisionStorage { get; private set; }
		public IOriginalStreamScavengeMap<ulong> OriginalStorage { get; private set; }
		public IOriginalStreamScavengeMap<TStreamId> OriginalCollisionStorage { get; private set; }
		public IScavengeMap<Unit,ScavengeCheckpoint> CheckpointStorage { get; private set; }
		public IScavengeMap<int,ChunkTimeStampRange> ChunkTimeStampRanges { get; private set; }
		public IChunkWeightScavengeMap ChunkWeights { get; private set; }
		public ITransactionFactory<SqliteTransaction> TransactionFactory { get; private set; }
		public ITransactionManager TransactionManager { get; private set; }

		public SqliteScavengeBackend(int cacheSizeInBytes = DefaultSqliteCacheSize) {
			Ensure.Positive(cacheSizeInBytes, nameof(cacheSizeInBytes));
			_cacheSizeInBytes = cacheSizeInBytes;
		}

		public void Dispose() {
			_sqliteBackend.Dispose();
		}

		public void Initialize(SqliteConnection connection) {
			_sqliteBackend = new SqliteBackend(connection);
			
			ConfigureFeatures();
			InitializeSchemaVersion();

			var collisionStorage = new SqliteCollisionScavengeMap<TStreamId>();
			CollisionStorage = collisionStorage;

			var hashes = new SqliteScavengeMap<ulong, TStreamId>("HashUsers");
			Hashes = hashes;

			var metaStorage = new SqliteMetastreamScavengeMap<ulong>("MetastreamDatas");
			MetaStorage = metaStorage;
			
			var metaCollisionStorage = new SqliteMetastreamScavengeMap<TStreamId>("MetastreamDataCollisions");
			MetaCollisionStorage = metaCollisionStorage;
			
			var originalStorage = new SqliteOriginalStreamScavengeMap<ulong>("OriginalStreamDatas");
			OriginalStorage = originalStorage;
			
			var originalCollisionStorage = new SqliteOriginalStreamScavengeMap<TStreamId>("OriginalStreamDataCollisions");
			OriginalCollisionStorage = originalCollisionStorage;
			
			var checkpointStorage = new SqliteScavengeCheckpointMap<TStreamId>();
			CheckpointStorage = checkpointStorage;
			
			var chunkTimeStampRanges = new SqliteChunkTimeStampRangeScavengeMap();
			ChunkTimeStampRanges = chunkTimeStampRanges;
			
			var chunkWeights = new SqliteChunkWeightScavengeMap();
			ChunkWeights = chunkWeights;

			var transactionFactory = new SqliteTransactionFactory();
			TransactionFactory = transactionFactory;

			TransactionManager = new SqliteTransactionManager(TransactionFactory, CheckpointStorage);

			var allMaps = new IInitializeSqliteBackend[] { collisionStorage, hashes, metaStorage, metaCollisionStorage,
				originalStorage, originalCollisionStorage, checkpointStorage, chunkTimeStampRanges, chunkWeights,
				transactionFactory};

			foreach (var map in allMaps) {
				map.Initialize(_sqliteBackend);
			}
		}

		private void ConfigureFeatures() {
			_sqliteBackend.SetPragmaValue(SqliteBackend.PageSize, SqlitePageSize.ToString());
			var pageSize = int.Parse(_sqliteBackend.GetPragmaValue(SqliteBackend.PageSize));
			if (pageSize != SqlitePageSize) {
				// note we will fail to set it if the database already exists with a different page size
				throw new Exception($"Failed to configure page size to {SqlitePageSize}. Actually {pageSize}");
			}
			
			_sqliteBackend.SetPragmaValue(SqliteBackend.JournalMode, SqliteWalJournalMode);
			var journalMode = _sqliteBackend.GetPragmaValue(SqliteBackend.JournalMode);
			if (journalMode.ToLower() != SqliteWalJournalMode) {
				throw new Exception($"Failed to configure journal mode, unexpected value: {journalMode}");
			}
			
			_sqliteBackend.SetPragmaValue(SqliteBackend.Synchronous, SqliteNormalSynchronousValue.ToString());
			var synchronousMode = int.Parse(_sqliteBackend.GetPragmaValue(SqliteBackend.Synchronous));
			if (synchronousMode != SqliteNormalSynchronousValue) {
				throw new Exception($"Failed to configure synchronous mode, unexpected value: {synchronousMode}");
			}

			_sqliteBackend.SetCacheSize(_cacheSizeInBytes);
		}

		private void InitializeSchemaVersion() {
			var tableName = "SchemaVersion";

			using (var cmd = _sqliteBackend.CreateCommand()) {
				cmd.CommandText = $"CREATE TABLE IF NOT EXISTS {tableName} (version Integer PRIMARY KEY)";
				cmd.ExecuteNonQuery();

				cmd.CommandText = $"SELECT MAX(version) FROM {tableName}";
				var currentVersion = cmd.ExecuteScalar();

				if (currentVersion == DBNull.Value) {
					cmd.CommandText = $"INSERT INTO {tableName} VALUES({SchemaVersion})";
					cmd.ExecuteNonQuery();
				} else if (currentVersion != null && (long)currentVersion < SchemaVersion) {
					// need schema update
				}
			}
		}

		public SqliteBackend.Stats GetStats() {
			return _sqliteBackend.GetStats();
		}

		public void LogStats() {
			Log.Trace($"SCAVENGING: {GetStats().PrettyPrint()}");
		}
	}
}
