using System.Linq;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging
{
    [TestFixture]
    public class when_stream_is_deleted : ScavengeTestScenario
    {
        protected override DbResult CreateDb(TFChunkDbCreationHelper dbCreator)
        {
            return dbCreator
                    .Chunk(Rec.Create(0, "bla"), Rec.Prepare(0, "bla"), Rec.Commit(0, "bla"))
                    .Chunk(Rec.Delete(1, "bla"), Rec.Commit(1, "bla"))
                    .CompleteLastChunk()
                    .CreateDb();
        }

        protected override LogRecord[][] KeptRecords(DbResult dbResult)
        {
            return new []
            {
                dbResult.Recs[0].Where((x, i) => i != 1).ToArray(),
                dbResult.Recs[1]
            };
        }

        [Test]
        public void stream_created_and_delete_tombstone_with_corresponding_commits_are_kept()
        {
            CheckRecords();
        }
    }
}