using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using NUnit.Framework;
using NUnit.Framework.Internal;
using EventStore.Core.Services.Storage.EpochManager;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using System.Threading;

namespace EventStore.Core.Tests.Services.Storage {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class when_having_TFLog_with_existing_epochs<TLogFormat, TStreamId> : SpecificationWithDirectoryPerTestFixture, IDisposable {
		private TFChunkDb _db;
		private EpochManager _epochManager;
		private LinkedList<EpochRecord> _cache;
		private TFChunkReader _reader;
		private TFChunkWriter _writer;
		private IBus _mainBus;
		private readonly Guid _instanceId = Guid.NewGuid();
		private readonly List<Message> _published = new List<Message>();
		private List<EpochRecord> _epochs;
		private static LogFormatAbstractor<TStreamId> _logFormat = LogFormatHelper<TLogFormat, TStreamId>.LogFormat;

		private int GetNextEpoch() {
			return (int)Interlocked.Increment(ref _currentEpoch);
		}
		private long _currentEpoch = -1;
		private EpochManager GetManager() {
			return new EpochManager(_mainBus,
				10,
				_db.Config.EpochCheckpoint,
				_writer,
				initialReaderCount: 1,
				maxReaderCount: 5,
				readerFactory: () => new TFChunkReader(_db, _db.Config.WriterCheckpoint,
					optimizeReadSideCache: _db.Config.OptimizeReadSideCache),
				_logFormat.RecordFactory,
				_instanceId);
		}
		private LinkedList<EpochRecord> GetCache(EpochManager manager) {
			return (LinkedList<EpochRecord>)typeof(EpochManager).GetField("_epochs", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(_epochManager);
		}
		private EpochRecord WriteEpoch(int epochNumber, long lastPos, Guid instanceId) {
			long pos = _writer.Checkpoint.ReadNonFlushed();
			var epoch = new EpochRecord(pos, epochNumber, Guid.NewGuid(), lastPos, DateTime.UtcNow, instanceId);
			var rec = _logFormat.RecordFactory.CreateEpoch(epoch);
			_writer.Write(rec, out _);
			_writer.Flush();
			return epoch;
		}
		[OneTimeSetUp]
		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();
			_mainBus = new InMemoryBus(nameof(when_having_an_epoch_manager_and_empty_tf_log<TLogFormat, TStreamId>));
			_mainBus.Subscribe(new AdHocHandler<SystemMessage.EpochWritten>(m => _published.Add(m)));
			_db = new TFChunkDb(TFChunkHelper.CreateDbConfig(PathName, 0));
			_db.Open();
			_reader = new TFChunkReader(_db, _db.Config.WriterCheckpoint);
			_writer = new TFChunkWriter(_db);

			_epochManager = GetManager();
			_epochManager.Init();
			_cache = GetCache(_epochManager);
			Assert.NotNull(_cache);
			Assert.That(_cache.Count == 0);
			_epochs = new List<EpochRecord>();
			var lastPos = 0L;
			for (int i = 0; i < 30; i++) {
				var epoch = WriteEpoch(GetNextEpoch(), lastPos, _instanceId);
				_epochs.Add(epoch);
				lastPos = epoch.EpochPosition;
			}
		}

		[OneTimeTearDown]
		public override async Task TestFixtureTearDown() {
			this.Dispose();
			await base.TestFixtureTearDown();
		}
		// epoch manager is stateful with TFLog, 
		// and TFLog is expesive to build fresh for each test
		// and the tests depend on previous state in the epoch manager
		// so this test will run through the test cases 
		// in order
		[Test]
		public void can_add_epochs_to_cache() {

			Assert.That(_cache.Count == 0);
			//add fist epoch to empty cache
			_epochManager.AddEpochToCache(_epochs[3]);

			Assert.That(_cache.Count == 4);
			Assert.That(_cache.First.Value.EpochNumber == _epochs[0].EpochNumber);
			Assert.That(_cache.Last.Value.EpochNumber == _epochs[3].EpochNumber);

			//add new last epoch
			_epochManager.AddEpochToCache(_epochs[4]);

			Assert.That(_cache.Count == 5);
			Assert.That(_cache.First.Value.EpochNumber == _epochs[0].EpochNumber);
			Assert.That(_cache.Last.Value.EpochNumber == _epochs[4].EpochNumber);

			//idempotent add
			_epochManager.AddEpochToCache(_epochs[1]);
			_epochManager.AddEpochToCache(_epochs[2]);
			_epochManager.AddEpochToCache(_epochs[3]);

			Assert.That(_cache.Count == 5);
			Assert.That(_cache.First.Value.EpochNumber == _epochs[0].EpochNumber);
			Assert.That(_cache.Last.Value.EpochNumber == _epochs[4].EpochNumber);

			//add new skip 1 last epoch
			_epochManager.AddEpochToCache(_epochs[6]);

			Assert.That(_cache.Count == 7);
			Assert.That(_cache.First.Value.EpochNumber == _epochs[0].EpochNumber);
			Assert.That(_cache.Last.Value.EpochNumber == _epochs[6].EpochNumber);

			//add new skip 5 last epoch
			_epochManager.AddEpochToCache(_epochs[11]);

			Assert.That(_cache.Count == 10);
			Assert.That(_cache.First.Value.EpochNumber == _epochs[2].EpochNumber);
			Assert.That(_cache.Last.Value.EpochNumber == _epochs[11].EpochNumber);

			//add last rolls cache
			_epochManager.AddEpochToCache(_epochs[12]);

			Assert.That(_cache.Count == 10);
			Assert.That(_cache.First.Value.EpochNumber == _epochs[3].EpochNumber);
			Assert.That(_cache.Last.Value.EpochNumber == _epochs[12].EpochNumber);


			//add epoch before cache
			_epochManager.AddEpochToCache(_epochs[1]);

			Assert.That(_cache.Count == 10);
			Assert.That(_cache.First.Value.EpochNumber == _epochs[3].EpochNumber);
			Assert.That(_cache.Last.Value.EpochNumber == _epochs[12].EpochNumber);

			//add idempotent first epoch
			_epochManager.AddEpochToCache(_epochs[2]);

			Assert.That(_cache.Count == 10);
			Assert.That(_cache.First.Value.EpochNumber == _epochs[3].EpochNumber);
			Assert.That(_cache.Last.Value.EpochNumber == _epochs[12].EpochNumber);

			//add idempotent last epoch
			_epochManager.AddEpochToCache(_epochs[12]);

			Assert.That(_cache.Count == 10);
			Assert.That(_cache.First.Value.EpochNumber == _epochs[3].EpochNumber);
			Assert.That(_cache.Last.Value.EpochNumber == _epochs[12].EpochNumber);

			//add disjunct skip epoch
			_epochManager.AddEpochToCache(_epochs[24]);

			Assert.That(_cache.Count == 10);
			Assert.That(_cache.First.Value.EpochNumber == _epochs[15].EpochNumber);
			Assert.That(_cache.Last.Value.EpochNumber == _epochs[24].EpochNumber);

			
			//cannot get epoch ahead of last cached on master
			var nextEpoch = _epochManager.GetEpochAfter(_epochs[24].EpochNumber, false);
			Assert.Null(nextEpoch);

			Assert.That(_cache.Count == 10);
			Assert.That(_cache.First.Value.EpochNumber == _epochs[15].EpochNumber);
			Assert.That(_cache.Last.Value.EpochNumber == _epochs[24].EpochNumber);

			//cannot get epoch ahead of cache on master
			nextEpoch = _epochManager.GetEpochAfter(_epochs[25].EpochNumber, false);
			Assert.Null(nextEpoch);

			Assert.That(_cache.Count == 10);
			Assert.That(_cache.First.Value.EpochNumber == _epochs[15].EpochNumber);
			Assert.That(_cache.Last.Value.EpochNumber == _epochs[24].EpochNumber);
						
			//can get next  in cache			
			nextEpoch = _epochManager.GetEpochAfter(_epochs[20].EpochNumber, false);
			
			Assert.That(nextEpoch.EpochPosition == _epochs[21].EpochPosition);
			Assert.That(_cache.Count == 10);
			Assert.That(_cache.First.Value.EpochNumber == _epochs[15].EpochNumber);
			Assert.That(_cache.Last.Value.EpochNumber == _epochs[24].EpochNumber);
			
			//can get next from first			
			nextEpoch = _epochManager.GetEpochAfter(_epochs[15].EpochNumber, false);
			
			Assert.That(nextEpoch.EpochPosition == _epochs[16].EpochPosition);
			Assert.That(_cache.Count == 10);
			Assert.That(_cache.First.Value.EpochNumber == _epochs[15].EpochNumber);
			Assert.That(_cache.Last.Value.EpochNumber == _epochs[24].EpochNumber);
			
			//can get next epoch from just before cache 
			nextEpoch = _epochManager.GetEpochAfter(_epochs[14].EpochNumber, false);
			
			Assert.That(nextEpoch.EpochPosition == _epochs[15].EpochPosition);
			Assert.That(_cache.Count == 10);
			Assert.That(_cache.First.Value.EpochNumber == _epochs[15].EpochNumber);
			Assert.That(_cache.Last.Value.EpochNumber == _epochs[24].EpochNumber);

			//can get next epoch from before cache 
			nextEpoch = _epochManager.GetEpochAfter(_epochs[10].EpochNumber, false);
			
			Assert.That(nextEpoch.EpochPosition == _epochs[11].EpochPosition);
			Assert.That(_cache.Count == 10);
			Assert.That(_cache.First.Value.EpochNumber == _epochs[15].EpochNumber);
			Assert.That(_cache.Last.Value.EpochNumber == _epochs[24].EpochNumber);

			//can get next epoch from 0 epoch
			nextEpoch = _epochManager.GetEpochAfter(_epochs[0].EpochNumber, false);
			
			Assert.That(nextEpoch.EpochPosition == _epochs[1].EpochPosition);
			Assert.That(_cache.Count == 10);
			Assert.That(_cache.First.Value.EpochNumber == _epochs[15].EpochNumber);
			Assert.That(_cache.Last.Value.EpochNumber == _epochs[24].EpochNumber);


			//can add last epoch in log
			_epochManager.AddEpochToCache(_epochs[29]);

			Assert.That(_cache.Count == 10);
			Assert.That(_cache.First.Value.EpochNumber == _epochs[20].EpochNumber);
			Assert.That(_cache.Last.Value.EpochNumber == _epochs[29].EpochNumber);

		}
		public void Dispose() {
			//epochManager?.Dispose();
			//reader?.Dispose();
			try {
				_writer?.Dispose();
			} catch {
				//workaround for TearDown error
			}
			_db?.Dispose();
		}
	}
}
