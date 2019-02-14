using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.DeletingStream {
	[TestFixture]
	public class when_deleting_stream_with_version_numbers_greater_than_int_maxvalue : ReadIndexTestScenario {
		long firstEventNumber = (long)int.MaxValue + 1;
		long secondEventNumber = (long)int.MaxValue + 2;
		long thirdEventNumber = (long)int.MaxValue + 3;

		protected override void WriteTestScenario() {
			WriteSingleEvent("ES", firstEventNumber, new string('.', 3000));
			WriteSingleEvent("KEEP", firstEventNumber, new string('.', 3000));
			WriteSingleEvent("KEEP", secondEventNumber, new string('.', 3000));
			WriteSingleEvent("ES", secondEventNumber, new string('.', 3000), retryOnFail: true);
			WriteSingleEvent("KEEP", thirdEventNumber, new string('.', 3000));
			WriteSingleEvent("ES", thirdEventNumber, new string('.', 3000));

			WriteDelete("ES");
		}

		[Test]
		public void indicate_that_stream_is_deleted() {
			Assert.That(ReadIndex.IsStreamDeleted("ES"));
		}

		[Test]
		public void indicate_that_other_stream_is_not_deleted() {
			Assert.IsFalse(ReadIndex.IsStreamDeleted("KEEP"));
		}
	}
}
