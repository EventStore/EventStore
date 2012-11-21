using System.Linq;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging
{
    [TestFixture]
    public class when_deleting_stream_with_a_lot_of_data : ScavengeTestScenario
    {
        protected override DbResult CreateDb(TFChunkDbCreationHelper dbCreator)
        {
            return dbCreator
                    .Chunk(Rec.Create(0, "bla"),
                           Rec.Prepare(0, "bla"),
                           Rec.Commit(0, "bla"),
                           Rec.Prepare(1, "bla"),
                           Rec.Prepare(1, "bla"),
                           Rec.Prepare(1, "bla"),
                           Rec.Prepare(1, "bla"),
                           Rec.Commit(1, "bla"),
                           Rec.Delete(2, "bla"),
                           Rec.Commit(2, "bla"))
                    .CompleteLastChunk()
                    .CreateDb();
        }

        protected override LogRecord[][] KeptRecords(DbResult dbResult)
        {
            return new[]
                   {
                           dbResult.Recs[0].Where((x, i) => new[]{0,2,8,9}.Contains(i)).ToArray(),
                   };
        }

        [Test]
        public void only_stream_created_and_delete_tombstone_records_with_their_commits_are_kept()
        {
            CheckRecords();
        }
    }
}