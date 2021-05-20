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
	public class when_having_an_epoch_manager_and_empty_tf_log<TLogFormat, TStreamId> : SpecificationWithDirectoryPerTestFixture, IDisposable {
		private TFChunkDb _db;
		private EpochManager _epochManager;
		private LinkedList<EpochRecord> _cache;
		private TFChunkReader _reader;
		private TFChunkWriter _writer;
		private IBus _mainBus;
		private readonly Guid _instanceId = Guid.NewGuid();
		private readonly List<Message> _published = new List<Message>();

		private int GetNextEpoch() {
			return (int)Interlocked.Increment(ref _currentEpoch);
		}
		private long _currentEpoch = -1;
		private EpochManager GetManager() {
			var logFormat = LogFormatHelper<TLogFormat, TStreamId>.LogFormat;

			return new EpochManager(_mainBus,
				10,
				_db.Config.EpochCheckpoint,
				_writer,
				initialReaderCount: 1,
				maxReaderCount: 5,
				readerFactory: () => new TFChunkReader(_db, _db.Config.WriterCheckpoint,
					optimizeReadSideCache: _db.Config.OptimizeReadSideCache),
				logFormat.RecordFactory,
				_instanceId);
		}
		private LinkedList<EpochRecord> GetCache(EpochManager manager) {
			return (LinkedList<EpochRecord>)typeof(EpochManager).GetField("_epochs", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(_epochManager);
		}
		private EpochRecord WriteEpoch(int epochNumber, long lastPos, Guid instanceId) {
			long pos = _writer.Checkpoint.ReadNonFlushed();
			var epoch = new EpochRecord(pos, epochNumber, Guid.NewGuid(), lastPos, DateTime.UtcNow, instanceId);
			var rec = new SystemLogRecord(epoch.EpochPosition, epoch.TimeStamp, SystemRecordType.Epoch,
				SystemRecordSerialization.Json, epoch.AsSerialized());
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
		}

		[OneTimeTearDown]
		public override Task TestFixtureTearDown() => base.TestFixtureTearDown();

		// epoch manager is stateful with TFLog,
		// and TFLog is expesive to build fresh for each test
		// and the tests depend on previous state in the epoch manager
		// so this test will run through the test cases 
		// in order
		[Test]
		public void can_write_epochs() {

			//can write first epoch
			_published.Clear();
			var beforeWrite = DateTime.UtcNow;
			_epochManager.WriteNewEpoch(GetNextEpoch());
			Assert.That(_published.Count == 1);
			var epochWritten = _published[0] as SystemMessage.EpochWritten;
			Assert.NotNull(epochWritten);
			Assert.That(epochWritten.Epoch.EpochNumber == 0);
			Assert.That(epochWritten.Epoch.PrevEpochPosition == -1);
			Assert.That(epochWritten.Epoch.EpochPosition == 0);
			Assert.That(epochWritten.Epoch.LeaderInstanceId == _instanceId);
			Assert.That(epochWritten.Epoch.TimeStamp < DateTime.UtcNow);
			Assert.That(epochWritten.Epoch.TimeStamp >= beforeWrite);
			_published.Clear();

			// will_cache_epochs_written() {
			
			for (int i = 0; i < 4; i++) {
				_epochManager.WriteNewEpoch(GetNextEpoch());
			}
			Assert.That(_cache.Count == 5);
			Assert.That(_cache.First.Value.EpochNumber == 0);
			Assert.That(_cache.Last.Value.EpochNumber == 4);
			var epochs = new List<int>();
			var epoch = _cache.First;
			while (epoch != null) {
				epochs.Add(epoch.Value.EpochNumber);
				epoch = epoch.Next;
			}
			CollectionAssert.IsOrdered(epochs);

			// can_write_more_epochs_than_cache_size
			
			for (int i = 0; i < 16; i++) {
				_epochManager.WriteNewEpoch(GetNextEpoch());
			}
			Assert.That(_cache.Count == 10);
			Assert.That(_cache.First.Value.EpochNumber == 11);
			Assert.That(_cache.Last.Value.EpochNumber == 20);			
			epochs = new List<int>();
			epoch = _cache.First;
			while (epoch != null) {
				epochs.Add(epoch.Value.EpochNumber);
				epoch = epoch.Next;
			}
			CollectionAssert.IsOrdered(epochs);
		}

		public void Dispose() {
			//epochManager?.Dispose();
			//reader?.Dispose();
			_writer?.Dispose();
			_db?.Dispose();
		}
	}
}
