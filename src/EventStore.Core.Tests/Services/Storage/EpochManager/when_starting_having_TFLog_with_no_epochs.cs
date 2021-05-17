using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.LogV2;
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
using EventStore.Core.LogAbstraction;

namespace EventStore.Core.Tests.Services.Storage {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public sealed class when_starting_having_TFLog_with_no_epochs<TLogFormat, TStreamId> : SpecificationWithDirectoryPerTestFixture, IDisposable {
		private TFChunkDb _db;
		private EpochManager _epochManager;
		private LinkedList<EpochRecord> _cache;
		private TFChunkReader _reader;
		private TFChunkWriter _writer;
		private IBus _mainBus;
		private readonly Guid _instanceId = Guid.NewGuid();
		private readonly List<Message> _published = new List<Message>();
		private readonly LogFormatAbstractor<TStreamId> _logFormat;
		public when_starting_having_TFLog_with_no_epochs() {
			_logFormat = LogFormatHelper<TLogFormat, TStreamId>.LogFormat;
		}

		private static int GetNextEpoch() {
			return (int)Interlocked.Increment(ref _currentEpoch);
		}
		private static long _currentEpoch = -1;
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
			var rec = new SystemLogRecord(epoch.EpochPosition, epoch.TimeStamp, SystemRecordType.Epoch,
				SystemRecordSerialization.Json, epoch.AsSerialized());
			_writer.Write(rec, out _);
			_writer.Flush();
			return epoch;
		}
		[OneTimeSetUp]
		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();
			_mainBus = new InMemoryBus(nameof(when_starting_having_TFLog_with_no_epochs<TLogFormat, TStreamId>));
			_mainBus.Subscribe(new AdHocHandler<SystemMessage.EpochWritten>(m => _published.Add(m)));
			_db = new TFChunkDb(TFChunkHelper.CreateDbConfig(PathName, 0));
			_db.Open();
			_reader = new TFChunkReader(_db, _db.Config.WriterCheckpoint);
			_writer = new TFChunkWriter(_db);			
			
		}

		[OneTimeTearDown]
		public override async Task TestFixtureTearDown() {
			this.Dispose();
			await base.TestFixtureTearDown();
		}
		
		[Test]
		public void starting_epoch_manager_loads_without_epochs() {

			_epochManager = GetManager();
			_epochManager.Init();
			_cache = GetCache(_epochManager);
			Assert.NotNull(_cache);
			
			Assert.That(_cache.Count == 0);
			Assert.That(_cache?.First?.Value == null);
			Assert.That(_cache?.Last?.Value == null);
			Assert.That(_epochManager.LastEpochNumber == -1);
			_epochManager.WriteNewEpoch(0);
			Assert.That(_cache.Count == 1);
			Assert.That(_cache.First.Value.EpochNumber == 0);
			Assert.That(_cache.Last.Value.EpochNumber == 0);
			Assert.That(_epochManager.LastEpochNumber == 0);

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
