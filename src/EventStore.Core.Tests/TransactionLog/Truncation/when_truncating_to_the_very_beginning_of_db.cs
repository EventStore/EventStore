﻿using System.IO;
using System.Threading.Tasks;
using EventStore.Core.Tests.TransactionLog;
using EventStore.Core.Tests.TransactionLog.Validation;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using EventStore.Core.TransactionLog.TestHelpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Truncation {
	[TestFixture]
	public class when_truncating_to_the_very_beginning_of_multichunk_db : SpecificationWithDirectoryPerTestFixture {
		private TFChunkDbConfig _config;

		[OneTimeSetUp]
		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();

			_config = TFChunkHelper.CreateDbConfig(PathName, 11111, 5500, 5500, 0, 1000);

			DbUtil.CreateMultiChunk(_config, 0, 2, GetFilePathFor("chunk-000000.000001"));
			DbUtil.CreateMultiChunk(_config, 0, 2, GetFilePathFor("chunk-000000.000002"));
			DbUtil.CreateMultiChunk(_config, 3, 10, GetFilePathFor("chunk-000003.000001"));
			DbUtil.CreateMultiChunk(_config, 3, 10, GetFilePathFor("chunk-000003.000002"));
			DbUtil.CreateMultiChunk(_config, 7, 8, GetFilePathFor("chunk-000007.000001"));
			DbUtil.CreateOngoingChunk(_config, 11, GetFilePathFor("chunk-000011.000000"));

			var truncator = new TFChunkDbTruncator(_config);
			truncator.TruncateDb(_config.TruncateCheckpoint.ReadNonFlushed());
		}

		[OneTimeTearDown]
		public override Task TestFixtureTearDown() {
			using (var db = new TFChunkDb(_config)) {
				Assert.DoesNotThrow(() => db.Open(verifyHash: false));
			}

			Assert.IsTrue(File.Exists(GetFilePathFor("chunk-000000.000000")));
			Assert.AreEqual(1, Directory.GetFiles(PathName, "*").Length);

			return base.TestFixtureTearDown();
		}

		[Test]
		public void writer_checkpoint_should_be_set_to_start_of_new_chunk() {
			Assert.AreEqual(0, _config.WriterCheckpoint.Read());
			Assert.AreEqual(0, _config.WriterCheckpoint.ReadNonFlushed());
		}

		[Test]
		public void chaser_checkpoint_should_be_adjusted_if_less_than_actual_truncate_checkpoint() {
			Assert.AreEqual(0, _config.ChaserCheckpoint.Read());
			Assert.AreEqual(0, _config.ChaserCheckpoint.ReadNonFlushed());
		}

		[Test]
		public void epoch_checkpoint_should_be_reset_if_less_than_actual_truncate_checkpoint() {
			Assert.AreEqual(-1, _config.EpochCheckpoint.Read());
			Assert.AreEqual(-1, _config.EpochCheckpoint.ReadNonFlushed());
		}

		[Test]
		public void truncate_checkpoint_should_be_reset_after_truncation() {
			Assert.AreEqual(-1, _config.TruncateCheckpoint.Read());
			Assert.AreEqual(-1, _config.TruncateCheckpoint.ReadNonFlushed());
		}

		[Test]
		public void all_chunks_should_be_deleted() {
			Assert.AreEqual(0, Directory.GetFiles(PathName, "*").Length);
		}
	}
}
