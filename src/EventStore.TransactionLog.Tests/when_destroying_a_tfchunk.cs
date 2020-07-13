using System.IO;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.TestHelpers;
using EventStore.Core.TransactionLog.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.TransactionLog.Tests {
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
