using System.IO;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.TestHelpers;
using EventStore.Core.TransactionLog.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.TransactionLog.Tests {
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
