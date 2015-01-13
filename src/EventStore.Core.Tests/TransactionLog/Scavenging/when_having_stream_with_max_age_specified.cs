using System;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging
{
    [TestFixture]
    public class when_having_stream_with_max_age_specified : ScavengeTestScenario
    {
        protected override DbResult CreateDb(TFChunkDbCreationHelper dbCreator)
        {
            return dbCreator
                    .Chunk(Rec.Prepare(0, "$$bla", metadata: new StreamMetadata(null, TimeSpan.FromMinutes(5), null, null, null)),
                           Rec.Commit(0, "$$bla"),
                           Rec.Prepare(3, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(110)),
                           Rec.Commit(3, "bla"),
                           Rec.Prepare(1, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(100)),
                           Rec.Prepare(1, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(90)),
                           Rec.Prepare(1, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(60)),
                           Rec.Prepare(1, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(40)),
                           Rec.Prepare(1, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(30)),
                           Rec.Commit(1, "bla"),
                           Rec.Prepare(2, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(20)),
                           Rec.Prepare(2, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(3)),
                           Rec.Prepare(2, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(2)),
                           Rec.Prepare(2, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(1)),
                           Rec.Commit(2, "bla"))
                    .CompleteLastChunk()
                    .CreateDb();
        }

        protected override LogRecord[][] KeptRecords(DbResult dbResult)
        {
            return new[]
            {
                dbResult.Recs[0].Where((x, i) => new[] {0, 1, 11, 12, 13, 14}.Contains(i)).ToArray()
            };
        }

        [Test]
        public void expired_prepares_are_scavenged()
        {
            CheckRecords();
        }
    }

    [TestFixture]
    public class when_having_stream_with_truncate_before_specified : ScavengeTestScenario
    {
        protected override DbResult CreateDb(TFChunkDbCreationHelper dbCreator)
        {
            return dbCreator
                    .Chunk(Rec.Prepare(0, "$$bla", metadata: new StreamMetadata(null, null, 0, null, null)),
                           Rec.Commit(0, "$$bla"),
                           Rec.Prepare(3, "bla"),
                           Rec.Commit(3, "bla"),
                           Rec.Prepare(1, "bla"),
                           Rec.Prepare(1, "bla"),
                           Rec.Prepare(1, "bla"),
                           Rec.Prepare(1, "bla"),
                           Rec.Prepare(1, "bla"),
                           Rec.Commit(1, "bla"),
                           Rec.Prepare(2, "bla"),
                           Rec.Prepare(2, "bla"),
                           Rec.Prepare(2, "bla"),
                           Rec.Prepare(2, "bla"),
                           Rec.Commit(2, "bla"),
                           Rec.Prepare(4, "$$bla", metadata: new StreamMetadata(null, null, 10, null, null)),
                           Rec.Commit(4, "$$bla")                           )
                    .CompleteLastChunk()
                    .CreateDb();
        }

        protected override LogRecord[][] KeptRecords(DbResult dbResult)
        {
            return new[]
            {
                dbResult.Recs[0].Where((x, i) => new[] {0, 1, 15, 16}.Contains(i)).ToArray()
            };
        }

        [Test]
        public void expired_prepares_are_scavenged()
        {
            CheckRecords();
        }
    }
}