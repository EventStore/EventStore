using NUnit.Framework;

namespace EventStore.Core.Tests.Infrastructure.Services.Storage.IsStreamDeleted
{
    [TestFixture]
    public class when_deleting_stream_with_2_hash_collisions_and_mixed_order_read_index_should : ReadIndexTestScenario
    {
        protected override void WriteTestScenario()
        {
            WriteStreamCreated("S1");
            WriteSingleEvent("S1", 1, "bla1");
            WriteStreamCreated("S2");
            WriteSingleEvent("S1", 2, "bla1");
            WriteSingleEvent("S2", 1, "bla1");
            WriteSingleEvent("S2", 2, "bla1");
            WriteStreamCreated("S3");
            WriteSingleEvent("S1", 3, "bla1");
            WriteSingleEvent("S3", 1, "bla1");

            WriteDelete("S1");
        }

        [Test]
        public void indicate_that_stream_is_deleted()
        {
            Assert.That(ReadIndex.IsStreamDeleted("S1"));
        }

        [Test]
        public void indicate_that_other_streams_with_same_hash_are_not_deleted()
        {
            Assert.That(ReadIndex.IsStreamDeleted("S2"), Is.False);
            Assert.That(ReadIndex.IsStreamDeleted("S3"), Is.False);
        }

        [Test]
        public void indicate_that_not_existing_stream_with_same_hash_is_not_deleted()
        {
            Assert.That(ReadIndex.IsStreamDeleted("XX"), Is.False);
        }

        [Test]
        public void indicate_that_not_existing_stream_with_different_hash_is_not_deleted()
        {
            Assert.That(ReadIndex.IsStreamDeleted("XXX"), Is.False);
        }
    }
}