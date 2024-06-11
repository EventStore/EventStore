﻿using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Core.Tests.TransactionLog.Validation;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.Transforms;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Truncation {
	[TestFixture]
	public class when_truncating_to_a_data_chunk_boundary : SpecificationWithDirectoryPerTestFixture {
		private TFChunkDbConfig _config;

		private const int ChunkSize = 1000;
		private  const long TruncateChk = ChunkSize * 3;

		[OneTimeSetUp]
		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();

			_config = TFChunkHelper.CreateDbConfigEx(PathName, 11111, 5500, 5500, -1, TruncateChk, ChunkSize, -1);

			DbUtil.CreateMultiChunk(_config, 0, 2, GetFilePathFor("chunk-000000.000001"));
			DbUtil.CreateMultiChunk(_config, 0, 2, GetFilePathFor("chunk-000000.000002"));
			DbUtil.CreateSingleChunk(_config, 3, GetFilePathFor("chunk-000003.000000"));
			DbUtil.CreateSingleChunk(_config, 4, GetFilePathFor("chunk-000004.000000"));
			DbUtil.CreateOngoingChunk(_config, 5, GetFilePathFor("chunk-000005.000000"));

			var truncator = new TFChunkDbTruncator(_config, _ => IChunkTransformFactory.Identity);
			truncator.TruncateDb(_config.TruncateCheckpoint.ReadNonFlushed());
		}

		[OneTimeTearDown]
		public override Task TestFixtureTearDown() {
			using (var db = new TFChunkDb(_config)) {
				Assert.DoesNotThrow(() => db.Open(verifyHash: false));
			}

			Assert.IsTrue(File.Exists(GetFilePathFor("chunk-000000.000002")));
			Assert.IsTrue(File.Exists(GetFilePathFor("chunk-000003.000000")));
			Assert.AreEqual(2, Directory.GetFiles(PathName, "*").Length);

			return base.TestFixtureTearDown();
		}

		[Test]
		public void chunk_at_boundary_should_be_deleted() {
			var files = Directory.GetFiles(PathName, "*").Select(Path.GetFileName).Order().ToArray();
			Assert.AreEqual(new[] { "chunk-000000.000001", "chunk-000000.000002" }, files);
		}
	}
}
