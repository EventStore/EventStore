using System.IO;
using EventStore.Core.TransactionLogV2.Chunks.TFChunk;
using EventStore.Core.TransactionLogV2.TestHelpers;
using EventStore.Core.TransactionLogV2.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.TransactionLogV2.Tests {
	[TestFixture]
	public class when_destroying_a_tfchunk : SpecificationWithFile {
		private TFChunk _chunk;

		[SetUp]
		public override void SetUp() {
			base.SetUp();
			_chunk = TFChunkHelper.CreateNewChunk(Filename, 1000);
			_chunk.MarkForDeletion();
		}

		[Test]
		public void the_file_is_deleted() {
			Assert.IsFalse(File.Exists(Filename));
		}
	}
}
