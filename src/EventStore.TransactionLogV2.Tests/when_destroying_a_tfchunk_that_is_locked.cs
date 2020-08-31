using System.IO;
using EventStore.Core.TransactionLogV2.Chunks;
using EventStore.Core.TransactionLogV2.Chunks.TFChunk;
using EventStore.Core.TransactionLogV2.TestHelpers;
using EventStore.Core.TransactionLogV2.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.TransactionLogV2.Tests {
	[TestFixture]
	public class when_destroying_a_tfchunk_that_is_locked : SpecificationWithFile {
		private TFChunk _chunk;
		private TFChunkBulkReader _reader;

		[SetUp]
		public override void SetUp() {
			base.SetUp();
			_chunk = TFChunkHelper.CreateNewChunk(Filename, 1000);
			_reader = _chunk.AcquireReader();
			_chunk.MarkForDeletion();
		}

		[TearDown]
		public override void TearDown() {
			_reader.Release();
			_chunk.MarkForDeletion();
			_chunk.WaitForDestroy(2000);
			base.TearDown();
		}

		[Test]
		public void the_file_is_not_deleted() {
			Assert.IsTrue(File.Exists(Filename));
		}
	}
}
