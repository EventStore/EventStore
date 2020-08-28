using System.IO;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.TestHelpers;
using EventStore.Core.TransactionLog.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.TransactionLog.Tests {
	[TestFixture]
	public class when_marking_for_deletion_a_tfchunk_that_has_been_locked_and_unlocked : SpecificationWithFile {
		private TFChunk _chunk;

		[SetUp]
		public override void SetUp() {
			base.SetUp();
			_chunk = TFChunkHelper.CreateNewChunk(Filename, 1000);
			var reader = _chunk.AcquireReader();
			_chunk.MarkForDeletion();
			reader.Release();
		}

		[Test]
		public void the_file_is_deleted() {
			Assert.IsFalse(File.Exists(Filename));
		}
	}
}
