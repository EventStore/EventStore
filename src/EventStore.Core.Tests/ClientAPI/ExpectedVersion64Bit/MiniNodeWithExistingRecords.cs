using System;
using EventStore.ClientAPI;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.Tests.TransactionLog;
using NUnit.Framework;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Index;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.Util;
using System.IO;
using System.Threading.Tasks;

namespace EventStore.Core.Tests.ClientAPI.ExpectedVersion64Bit {
	public abstract class MiniNodeWithExistingRecords<TLogFormat, TStreamId> : SpecificationWithDirectoryPerTestFixture {
		private readonly TcpType _tcpType = TcpType.Ssl;
		protected MiniNode<TLogFormat, TStreamId> Node;

		protected readonly int MaxEntriesInMemTable = 20;
		protected readonly long MetastreamMaxCount = 1;
		protected readonly bool PerformAdditionalCommitChecks = true;
		protected readonly byte IndexBitnessVersion = Opts.IndexBitnessVersionDefault;
		protected TableIndex<TStreamId> TableIndex;
		protected IReadIndex<TStreamId> ReadIndex;

		protected TFChunkDb Db;
		protected TFChunkWriter Writer;
		protected ICheckpoint WriterCheckpoint;
		protected ICheckpoint ChaserCheckpoint;
		protected IODispatcher IODispatcher;
		protected InMemoryBus Bus;

		protected IEventStoreConnection _store;

		protected virtual IEventStoreConnection BuildConnection(MiniNode<TLogFormat, TStreamId> node) {
			return TestConnection<TLogFormat, TStreamId>.To(node, _tcpType);
		}

		[OneTimeSetUp]
		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();
			string dbPath = Path.Combine(PathName, string.Format("mini-node-db-{0}", Guid.NewGuid()));

			Bus = new InMemoryBus("bus");
			IODispatcher = new IODispatcher(Bus, new PublishEnvelope(Bus));

			if (!Directory.Exists(dbPath))
				Directory.CreateDirectory(dbPath);

			var writerCheckFilename = Path.Combine(dbPath, Checkpoint.Writer + ".chk");
			var chaserCheckFilename = Path.Combine(dbPath, Checkpoint.Chaser + ".chk");

			WriterCheckpoint = new MemoryMappedFileCheckpoint(writerCheckFilename, Checkpoint.Writer, cached: true);
			ChaserCheckpoint = new MemoryMappedFileCheckpoint(chaserCheckFilename, Checkpoint.Chaser, cached: true);

			Db = new TFChunkDb(TFChunkHelper.CreateDbConfig(dbPath, WriterCheckpoint, ChaserCheckpoint, TFConsts.ChunkSize));
			Db.Open();

			// create DB
			Writer = new TFChunkWriter(Db);
			Writer.Open();
			WriteTestScenario();

			Writer.Close();
			Writer = null;
			WriterCheckpoint.Flush();
			ChaserCheckpoint.Write(WriterCheckpoint.Read());
			ChaserCheckpoint.Flush();
			Db.Close();

			// start node with our created DB
			Node = new MiniNode<TLogFormat, TStreamId>(PathName, inMemDb: false, dbPath: dbPath);
			await Node.Start();

			try {
				await Given().WithTimeout();
			} catch (Exception ex) {
				throw new Exception("Given Failed", ex);
			}
		}

		[OneTimeTearDown]
		public override async Task TestFixtureTearDown() {
			_store?.Dispose();

			await Node.Shutdown();
			await base.TestFixtureTearDown();
		}

		public abstract void WriteTestScenario();
		public abstract Task Given();

		protected EventRecord WriteSingleEvent(string eventStreamName,
			long eventNumber,
			string data,
			DateTime? timestamp = null,
			Guid eventId = default(Guid),
			string eventType = "some-type") {
			var logFormat = LogFormatHelper<TLogFormat, TStreamId>.LogFormat;
			logFormat.StreamNameIndex.GetOrAddId(eventStreamName, out var eventStreamId, out _, out _);
			var prepare = LogRecord.SingleWrite(
				logFormat.RecordFactory,
				WriterCheckpoint.ReadNonFlushed(),
				eventId == default(Guid) ? Guid.NewGuid() : eventId,
				Guid.NewGuid(),
				eventStreamId,
				eventNumber - 1,
				eventType,
				Helper.UTF8NoBom.GetBytes(data),
				null,
				timestamp);
			long pos;
			Assert.IsTrue(Writer.Write(prepare, out pos));
			var commit = LogRecord.Commit(WriterCheckpoint.ReadNonFlushed(), prepare.CorrelationId, prepare.LogPosition,
				eventNumber);
			Assert.IsTrue(Writer.Write(commit, out pos));
			Assert.AreEqual(eventStreamId, prepare.EventStreamId);

			var eventRecord = new EventRecord(eventNumber, prepare, eventStreamName);
			return eventRecord;
		}
	}
}
