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

namespace EventStore.Core.Tests.ClientAPI.ExpectedVersion64Bit {
	public abstract class MiniNodeWithExistingRecords : SpecificationWithDirectoryPerTestFixture {
		private readonly TcpType _tcpType = TcpType.Normal;
		protected MiniNode Node;

		protected readonly int MaxEntriesInMemTable = 20;
		protected readonly long MetastreamMaxCount = 1;
		protected readonly bool PerformAdditionalCommitChecks = true;
		protected readonly byte IndexBitnessVersion = Opts.IndexBitnessVersionDefault;
		protected TableIndex TableIndex;
		protected IReadIndex ReadIndex;

		protected TFChunkDb Db;
		protected TFChunkWriter Writer;
		protected ICheckpoint WriterCheckpoint;
		protected ICheckpoint ChaserCheckpoint;
		protected IODispatcher IODispatcher;
		protected InMemoryBus Bus;

		protected IEventStoreConnection _store;

		protected virtual IEventStoreConnection BuildConnection(MiniNode node) {
			return TestConnection.To(node, _tcpType);
		}

		[OneTimeSetUp]
		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();
			string dbPath = Path.Combine(PathName, string.Format("mini-node-db-{0}", Guid.NewGuid()));

			Bus = new InMemoryBus("bus");
			IODispatcher = new IODispatcher(Bus, new PublishEnvelope(Bus));

			if (!Directory.Exists(dbPath))
				Directory.CreateDirectory(dbPath);

			var writerCheckFilename = Path.Combine(dbPath, Checkpoint.Writer + ".chk");
			var chaserCheckFilename = Path.Combine(dbPath, Checkpoint.Chaser + ".chk");
			if (Runtime.IsMono) {
				WriterCheckpoint = new FileCheckpoint(writerCheckFilename, Checkpoint.Writer, cached: true);
				ChaserCheckpoint = new FileCheckpoint(chaserCheckFilename, Checkpoint.Chaser, cached: true);
			} else {
				WriterCheckpoint = new MemoryMappedFileCheckpoint(writerCheckFilename, Checkpoint.Writer, cached: true);
				ChaserCheckpoint = new MemoryMappedFileCheckpoint(chaserCheckFilename, Checkpoint.Chaser, cached: true);
			}

			Db = new TFChunkDb(TFChunkHelper.CreateDbConfig(dbPath, WriterCheckpoint, ChaserCheckpoint,
				TFConsts.ChunkSize));
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
			Node = new MiniNode(PathName, inMemDb: false, dbPath: dbPath);
			Node.Start();

			Given();
		}

		[OneTimeTearDown]
		public override void TestFixtureTearDown() {
			if (_store != null) {
				_store.Dispose();
			}

			Node.Shutdown();
			base.TestFixtureTearDown();
		}

		public abstract void WriteTestScenario();
		public abstract void Given();

		protected EventRecord WriteSingleEvent(string eventStreamId,
			long eventNumber,
			string data,
			DateTime? timestamp = null,
			Guid eventId = default(Guid),
			string eventType = "some-type") {
			var prepare = LogRecord.SingleWrite(WriterCheckpoint.ReadNonFlushed(),
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

			var eventRecord = new EventRecord(eventNumber, prepare);
			return eventRecord;
		}
	}
}
