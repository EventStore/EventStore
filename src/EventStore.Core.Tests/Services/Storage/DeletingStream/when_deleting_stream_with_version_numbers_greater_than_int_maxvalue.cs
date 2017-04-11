using System;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.DeletingStream
{
    [TestFixture]
    public class when_deleting_stream_with_version_numbers_greater_than_int_maxvalue : ReadIndexTestScenario
    {
        long firstEventNumber = (long)int.MaxValue + 1;
        long secondEventNumber = (long)int.MaxValue + 2;
        long thirdEventNumber = (long)int.MaxValue + 3;

        protected override void WriteTestScenario()
        {
            // Guid id, string streamId, long position, long expectedVersion, PrepareFlags? flags = null
            WriteSingleEventWithLogVersion1(Guid.NewGuid(), "ES", WriterCheckpoint.ReadNonFlushed(), firstEventNumber);
            WriteSingleEventWithLogVersion1(Guid.NewGuid(), "KEEP", WriterCheckpoint.ReadNonFlushed(), firstEventNumber);
            WriteSingleEventWithLogVersion1(Guid.NewGuid(), "KEEP", WriterCheckpoint.ReadNonFlushed(), secondEventNumber);
            WriteSingleEventWithLogVersion1(Guid.NewGuid(), "ES", WriterCheckpoint.ReadNonFlushed(), secondEventNumber);
            WriteSingleEventWithLogVersion1(Guid.NewGuid(), "KEEP", WriterCheckpoint.ReadNonFlushed(), thirdEventNumber);
            WriteSingleEventWithLogVersion1(Guid.NewGuid(), "ES", WriterCheckpoint.ReadNonFlushed(), thirdEventNumber);

            WriteDelete("ES");
        }

        [Test]
        public void indicate_that_stream_is_deleted()
        {
            Assert.That(ReadIndex.IsStreamDeleted("ES"));
        }

        [Test]
        public void indicate_that_other_stream_is_not_deleted()
        {
            Assert.IsFalse(ReadIndex.IsStreamDeleted("KEEP"));
        }
    }
}