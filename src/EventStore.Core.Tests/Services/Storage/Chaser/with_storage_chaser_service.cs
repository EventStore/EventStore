using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Replication;
using EventStore.Core.Services.Storage;
using EventStore.Core.Services.Storage.EpochManager;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Core.Tests.Authentication;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.Tests.Services.ElectionsService;
using EventStore.Core.Tests.Services.Transport.Tcp;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.Chaser {
	public abstract class with_storage_chaser_service : SpecificationWithDirectoryPerTestFixture {
		ICheckpoint WriterChk = new InMemoryCheckpoint(Checkpoint.Writer);
		ICheckpoint ChaserChk = new InMemoryCheckpoint(Checkpoint.Chaser);
		ICheckpoint EpochChk = new InMemoryCheckpoint(Checkpoint.Epoch, initValue: -1);
		ICheckpoint TruncateChk = new InMemoryCheckpoint(Checkpoint.Truncate, initValue: -1);
		ICheckpoint ReplicationCheckpoint = new InMemoryCheckpoint(-1);

		protected InMemoryBus Publisher = new InMemoryBus("publisher");
		protected StorageChaser Service;
		protected FakeIndexCommitterService IndexCommiter;
		protected IEpochManager EpochManager;
		protected TFChunkDb Db;
		protected TFChunkChaser Chaser;
		protected TFChunkWriter Writer;

		protected List<StorageMessage.PrepareAck> PrepareAcks = new List<StorageMessage.PrepareAck>();
		protected List<StorageMessage.CommitAck> CommitAcks = new List<StorageMessage.CommitAck>();
		protected List<CommitMessage.WrittenTo> LogWrittenTos = new List<CommitMessage.WrittenTo>();

		[OneTimeSetUp]
		public async override Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();
			Db = new TFChunkDb(CreateDbConfig());
			Db.Open();
			Chaser = new TFChunkChaser(Db, WriterChk, ChaserChk, false);
			Chaser.Open();
			Writer = new TFChunkWriter(Db);
			Writer.Open();

			IndexCommiter = new FakeIndexCommitterService();
			EpochManager = new FakeEpochManager();

			Service = new StorageChaser(
				Publisher,
				WriterChk,
				Chaser,
				IndexCommiter,
				EpochManager,
				new QueueStatsManager());

			Service.Handle(new SystemMessage.SystemStart());
			Service.Handle(new SystemMessage.SystemInit());

			Publisher.Subscribe(new AdHocHandler<StorageMessage.CommitAck>(CommitAcks.Add));
			Publisher.Subscribe(new AdHocHandler<StorageMessage.PrepareAck>(PrepareAcks.Add));
			Publisher.Subscribe(new AdHocHandler<CommitMessage.WrittenTo>(LogWrittenTos.Add));


			When();
		}

		[OneTimeTearDown]
		public async override Task TestFixtureTearDown() {
			await base.TestFixtureTearDown();
			Service.Handle(new SystemMessage.BecomeShuttingDown(Guid.NewGuid(), true, true));
		}


		public abstract void When();

		private TFChunkDbConfig CreateDbConfig() {

			var nodeConfig = new TFChunkDbConfig(
				PathName, new VersionedPatternFileNamingStrategy(PathName, "chunk-"), 1000, 10000, WriterChk,
				ChaserChk, EpochChk, TruncateChk, ReplicationCheckpoint, Constants.TFChunkInitialReaderCountDefault, Constants.TFChunkMaxReaderCountDefault, true);
			return nodeConfig;
		}
	}
}
