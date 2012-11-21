using System.Linq;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging
{
    [TestFixture]
    public class when_deleting_stream_with_explicit_transaction : ScavengeTestScenario
    {
        protected override DbResult CreateDb(TFChunkDbCreationHelper dbCreator)
        {
            return dbCreator
                    .Chunk(Rec.TransSt(0, "bla"),
                           Rec.Create(0, "bla"), 
                           Rec.Prepare(0, "bla"),
                           Rec.Prepare(0, "bla"),
                           Rec.Prepare(0, "bla"),
                           Rec.TransEnd(0, "bla"),
                           Rec.Commit(0, "bla"),
                           Rec.Delete(1, "bla"),
                           Rec.Commit(1, "bla"))
                    .CompleteLastChunk()
                    .CreateDb();
        }

        protected override LogRecord[][] KeptRecords(DbResult dbResult)
        {
            return new[]
                   {
                           dbResult.Recs[0].Where((x, i) => new[] {1,6,7,8}.Contains(i)).ToArray(),
                   };
        }

        [Test]
        public void stream_created_and_delete_tombstone_with_commits_are_kept()
        {
            CheckRecords();
        }
    }
}