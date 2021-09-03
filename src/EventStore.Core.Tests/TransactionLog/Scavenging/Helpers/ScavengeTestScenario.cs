using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.DataStructures;
using EventStore.Core.Index;
using EventStore.Core.Index.Hashes;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Services;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using EventStore.Core.Util;
using EventStore.Core.LogAbstraction;

namespace EventStore.Core.Tests.TransactionLog.Scavenging.Helpers {
	[TestFixture]
	public abstract class ScavengeTestScenario<TLogFormat, TStreamId> : SpecificationWithDirectoryPerTestFixture {
		protected IReadIndex<TStreamId> ReadIndex;

		protected TFChunkDb Db {
			get { return _dbResult.Db; }
		}

		private readonly int _metastreamMaxCount;
		private DbResult _dbResult;
		private ILogRecord[][] _keptRecords;
		private ILogRecord[][] _originalRecords;

		private bool _checked;
		private LogFormatAbstractor<TStreamId> _logFormat;

		protected virtual bool UnsafeIgnoreHardDelete() {
			return false;
		}

		protected ScavengeTestScenario(int metastreamMaxCount = 1) {
			_metastreamMaxCount = metastreamMaxCount;
		}

		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();

			var indexDirectory = GetFilePathFor("index");
			_logFormat = LogFormatHelper<TLogFormat, TStreamId>.LogFormatFactory.Create(new() {
				IndexDirectory = indexDirectory,
			});

			var dbConfig = TFChunkHelper.CreateSizedDbConfig(PathName, 0, chunkSize: 1024 * 1024);
			var dbCreationHelper = new TFChunkDbCreationHelper<TLogFormat, TStreamId>(dbConfig, _logFormat);
			_dbResult = CreateDb(dbCreationHelper);
			_originalRecords = _dbResult.Recs;
			_keptRecords = KeptRecords(_dbResult);

			_dbResult.Db.Config.WriterCheckpoint.Flush();
			_dbResult.Db.Config.ChaserCheckpoint.Write(_dbResult.Db.Config.WriterCheckpoint.Read());
			_dbResult.Db.Config.ChaserCheckpoint.Flush();

			var readerPool = new ObjectPool<ITransactionFileReader>(
				"ReadIndex readers pool", Constants.PTableInitialReaderCount, Constants.PTableMaxReaderCountDefault,
				() => new TFChunkReader(_dbResult.Db, _dbResult.Db.Config.WriterCheckpoint));
			var lowHasher = _logFormat.LowHasher;
			var highHasher = _logFormat.HighHasher;
			var emptyStreamId = _logFormat.EmptyStreamId;
			var tableIndex = new TableIndex<TStreamId>(indexDirectory, lowHasher, highHasher, emptyStreamId,
				() => new HashListMemTable(PTableVersions.IndexV3, maxSize: 200),
				() => new TFReaderLease(readerPool),
				PTableVersions.IndexV3,
				5, Constants.PTableMaxReaderCountDefault,
				maxSizeForMemory: 100,
				maxTablesPerLevel: 2);
			_logFormat.StreamNamesProvider.SetTableIndex(tableIndex);
			var readIndex = new ReadIndex<TStreamId>(new NoopPublisher(), readerPool, tableIndex,
				_logFormat.StreamNameIndexConfirmer,
				_logFormat.StreamIds,
				_logFormat.StreamNamesProvider,
				_logFormat.EmptyStreamId,
				_logFormat.StreamIdValidator,
				_logFormat.StreamIdSizer,
				_logFormat.StreamExistenceFilter,
				_logFormat.StreamExistenceFilterReader,
				100, true, _metastreamMaxCount,
				Opts.HashCollisionReadLimitDefault, Opts.SkipIndexScanOnReadsDefault,
				_dbResult.Db.Config.ReplicationCheckpoint,_dbResult.Db.Config.IndexCheckpoint);
			readIndex.IndexCommitter.Init(_dbResult.Db.Config.WriterCheckpoint.Read());
			ReadIndex = readIndex;

			var scavenger = new TFChunkScavenger<TStreamId>(_dbResult.Db, new FakeTFScavengerLog(), tableIndex, ReadIndex,
				_logFormat.Metastreams,
				_logFormat.RecordFactory,
				unsafeIgnoreHardDeletes: UnsafeIgnoreHardDelete());
			await scavenger.Scavenge(alwaysKeepScavenged: true, mergeChunks: false);
		}

		public override async Task TestFixtureTearDown() {
			_logFormat?.Dispose();
			ReadIndex.Close();
			_dbResult.Db.Close();

			await base.TestFixtureTearDown();

			if (!_checked)
				throw new Exception("Records were not checked. Probably you forgot to call CheckRecords() method.");
		}

		protected abstract DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator);

		protected abstract ILogRecord[][] KeptRecords(DbResult dbResult);

		protected void CheckRecords() {
			_checked = true;
			Assert.AreEqual(_keptRecords.Length, _dbResult.Db.Manager.ChunksCount, "Wrong chunks count.");

			for (int i = 0; i < _keptRecords.Length; ++i) {
				var chunk = _dbResult.Db.Manager.GetChunk(i);

				var chunkRecords = new List<ILogRecord>();
				RecordReadResult result = chunk.TryReadFirst();
				while (result.Success) {
					chunkRecords.Add(result.LogRecord);
					result = chunk.TryReadClosestForward((int)result.NextPosition);
				}

				Assert.AreEqual(_keptRecords[i].Length, chunkRecords.Count, "Wrong number of records in chunk #{0}", i);

				for (int j = 0; j < _keptRecords[i].Length; ++j) {
					Assert.AreEqual(_keptRecords[i][j], chunkRecords[j], "Wrong log record #{0} read from chunk #{1}",
						j, i);
				}
			}

			//check if original log position maps to correct record
			var keptPositions = new HashSet<long>();
			for (int i = 0; i < _keptRecords.Length; i++) {
				for (int j = 0; j < _keptRecords[i].Length; j++) {
					var keptRecord = _keptRecords[i][j];
					keptPositions.Add(keptRecord.LogPosition);
					var recordReadResult = ReadLogRecordAt(keptRecord.LogPosition);
					Assert.True(recordReadResult.Success, $"Failed to read kept record #{j} from chunk #{i}");
					Assert.AreEqual(keptRecord, recordReadResult.LogRecord, $"Kept record #{j} from chunk #{i} does not match read record");

					if (keptRecord is IPrepareLogRecord<TStreamId> keptPrepare) {
						var originalPrepare = _originalRecords[i].First(x => x is IPrepareLogRecord<TStreamId> p
				                               && EqualityComparer<TStreamId>.Default.Equals(p.EventStreamId, keptPrepare.EventStreamId)
				                               && p.ExpectedVersion <= keptPrepare.ExpectedVersion
				                               && keptPrepare.ExpectedVersion < p.ExpectedVersion + p.Events.Length) as IPrepareLogRecord<TStreamId>;
						var eventOffset = keptPrepare.ExpectedVersion - originalPrepare!.ExpectedVersion;
						for (int k = 0; k < keptPrepare.Events.Length; k++) {
							//verify against the original log record that the event log positions are preserved
							Assert.AreEqual(originalPrepare!.Events[k + eventOffset].EventLogPosition!.Value, keptPrepare.Events[k].EventLogPosition!.Value,
								$"Original log position of event #{k} for kept record #{j} from chunk #{i} does not match with new log position");
							var eventReadResult = ReadLogRecordAt(keptPrepare.Events[k].EventLogPosition!.Value);
							Assert.True(eventReadResult.Success, $"Failed to read event #{k} for kept record #{j} from chunk #{i}");
							Assert.AreEqual(keptRecord, eventReadResult.LogRecord, $"Event #{k} for kept record #{j} from chunk #{i} does not match read record");
							keptPositions.Add(keptPrepare.Events[k].EventLogPosition!.Value);
						}
					}
				}
			}

			//check that deleted records are no longer readable
			for (int i = 0; i < _originalRecords.Length; i++) {
				for (int j = 0; j < _originalRecords[i].Length; j++) {
					var originalRecord = _originalRecords[i][j];
					if (keptPositions.Contains(originalRecord.LogPosition)) continue;
					var recordReadResult = ReadLogRecordAt(originalRecord.LogPosition);
					Assert.False(recordReadResult.Success, $"Succeeded to read deleted record #{j} from chunk #{i}");
					if (originalRecord is IPrepareLogRecord<TStreamId> prepare) {
						for (int k = 0; k < prepare.Events.Length; k++) {
							if (keptPositions.Contains(prepare.Events[k].EventLogPosition!.Value)) continue;
							var eventReadResult = ReadLogRecordAt(prepare.Events[k].EventLogPosition!.Value);
							Assert.False(eventReadResult.Success,  $"Succeeded to read event #{k} for deleted record #{j} from chunk #{i}");
						}
					}
				}
			}

			RecordReadResult ReadLogRecordAt(long logPosition) {
				var chunk = Db.Manager.GetChunkFor(logPosition);
				var localPos = chunk.ChunkHeader.GetLocalLogPosition(logPosition);
				var result = chunk.TryReadAt(localPos);
				return result;
			}

		}
	}
}
