using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.DeletingStream {
	[TestFixture]
	public class with_empty_db_read_index_should : ReadIndexTestScenario {
		protected override void WriteTestScenario() {
		}

		[Test]
		public void indicate_that_any_stream_is_not_deleted() {
			Assert.That(ReadIndex.IsStreamDeleted("X"), Is.False);
			Assert.That(ReadIndex.IsStreamDeleted("YY"), Is.False);
			Assert.That(ReadIndex.IsStreamDeleted("ZZZ"), Is.False);
		}
	}
}
