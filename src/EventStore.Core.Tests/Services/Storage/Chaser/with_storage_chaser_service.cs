﻿using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage;
using EventStore.Core.Services.Storage.EpochManager;
using EventStore.Core.Tests.Services.ElectionsService;
using EventStore.Core.TransactionLogV2.Checkpoint;
using EventStore.Core.TransactionLogV2.Chunks;
using EventStore.Core.TransactionLogV2.FileNamingStrategy;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.Chaser {
	public abstract class with_storage_chaser_service : SpecificationWithDirectoryPerTestFixture {
		readonly ICheckpoint _writerChk = new InMemoryCheckpoint(Checkpoint.Writer);
		readonly ICheckpoint _chaserChk = new InMemoryCheckpoint(Checkpoint.Chaser);
		readonly ICheckpoint _epochChk = new InMemoryCheckpoint(Checkpoint.Epoch, initValue: -1);
		readonly ICheckpoint _truncateChk = new InMemoryCheckpoint(Checkpoint.Truncate, initValue: -1);
		readonly ICheckpoint _replicationCheckpoint = new InMemoryCheckpoint(-1);
		readonly ICheckpoint _indexCheckpoint = new InMemoryCheckpoint(-1);

		protected InMemoryBus Publisher = new InMemoryBus("publisher");
		protected StorageChaser Service;
		protected FakeIndexCommitterService IndexCommitter;
		protected IEpochManager EpochManager;
		protected TFChunkDb Db;
		protected TFChunkChaser Chaser;
		protected TFChunkWriter Writer;

		protected ConcurrentQueue<StorageMessage.PrepareAck> PrepareAcks = new ConcurrentQueue<StorageMessage.PrepareAck>();
		protected ConcurrentQueue<StorageMessage.CommitAck> CommitAcks = new ConcurrentQueue<StorageMessage.CommitAck>();

		[OneTimeSetUp]
		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();
			Db = new TFChunkDb(CreateDbConfig());
			Db.Open();
			Chaser = new TFChunkChaser(Db, _writerChk, _chaserChk, false);
			Chaser.Open();
			Writer = new TFChunkWriter(Db);
			Writer.Open();

			IndexCommitter = new FakeIndexCommitterService();
			EpochManager = new FakeEpochManager();

			Service = new StorageChaser(
				Publisher,
				_writerChk,
				Chaser,
				IndexCommitter,
				EpochManager,
				new QueueStatsManager());

			Service.Handle(new SystemMessage.SystemStart());
			Service.Handle(new SystemMessage.SystemInit());

			Publisher.Subscribe(new AdHocHandler<StorageMessage.CommitAck>(CommitAcks.Enqueue));
			Publisher.Subscribe(new AdHocHandler<StorageMessage.PrepareAck>(PrepareAcks.Enqueue));

			When();
		}

		[OneTimeTearDown]
		public override async Task TestFixtureTearDown() {
			await base.TestFixtureTearDown();
			Service.Handle(new SystemMessage.BecomeShuttingDown(Guid.NewGuid(), true, true));
		}


		public abstract void When();

		private TFChunkDbConfig CreateDbConfig() {

			var nodeConfig = new TFChunkDbConfig(
				PathName, new VersionedPatternFileNamingStrategy(PathName, "chunk-"), 1000, 10000, _writerChk,
				_chaserChk, _epochChk, _truncateChk, _replicationCheckpoint, _indexCheckpoint, Constants.TFChunkInitialReaderCountDefault, Constants.TFChunkMaxReaderCountDefault, true);
			return nodeConfig;
		}
	}
}
