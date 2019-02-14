using EventStore.Core.Exceptions;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog {
	[TestFixture]
	public class when_opening_tfchunk_from_non_existing_file : SpecificationWithFile {
		[Test]
		public void it_should_throw_a_file_not_found_exception() {
			Assert.Throws<CorruptDatabaseException>(() => TFChunk.FromCompletedFile(Filename, verifyHash: true,
				unbufferedRead: false, initialReaderCount: 5, reduceFileCachePressure: false));
		}
	}
}
