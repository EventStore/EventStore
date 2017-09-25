using System.IO;
using EventStore.Core.Tests.TransactionLog;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog
{
    [TestFixture]
    public class when_opening_chunked_transaction_file_db_without_previous_files : SpecificationWithDirectory
    {
        [Test]
        public void with_a_writer_checksum_of_zero_the_first_chunk_is_created_with_correct_name_and_is_aligned()
        {
            var config = TFChunkDbConfigHelper.Create(PathName, 0);
            var db = new TFChunkDb(config);
            db.Open();
            db.Dispose();

            Assert.AreEqual(1, Directory.GetFiles(PathName).Length);
            Assert.IsTrue(File.Exists(GetFilePathFor("chunk-000000.000000")));
            var fileInfo = new FileInfo(GetFilePathFor("chunk-000000.000000"));
            Assert.AreEqual(12288, fileInfo.Length);
        }
    }
}