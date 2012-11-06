using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.IsStreamDeleted
{
    [TestFixture]
    public class when_deleting_the_only_existing_stream_in_db_read_index_should : ReadIndexTestScenario
    {

        protected override void WriteTestScenario()
        {
            WriteStreamCreated("ES");
            WriteSingleEvent("ES", 1, "bla1");

            WriteDelete("ES");
        }

        [Test]
        public void indicate_that_stream_is_deleted()
        {
            Assert.That(ReadIndex.IsStreamDeleted("ES"));
        }

        [Test]
        public void indicate_that_nonexisting_stream_with_same_hash_is_not_deleted()
        {
            Assert.That(ReadIndex.IsStreamDeleted("ZZ"), Is.False);
        }

        [Test]
        public void indicate_that_nonexisting_stream_with_different_hash_is_not_deleted()
        {
            Assert.That(ReadIndex.IsStreamDeleted("XXX"), Is.False);
        }
    }
}