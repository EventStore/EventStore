using System;
using System.Linq;
using System.Threading;
using EventStore.Core.Index;
using EventStore.Core.Index.Hashes;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV2
{
    [TestFixture, Category("LongRunning")]
    public class table_index_when_merging_upgrading_to_64bit_if_single_stream_entry_doesnt_exist_drops_entry_and_carries_on : SpecificationWithDirectoryPerTestFixture
    {
        private TableIndex _tableIndex;
        private IHasher _lowHasher;
        private IHasher _highHasher;
        private string _indexDir;
        protected byte _ptableVersion;

        public table_index_when_merging_upgrading_to_64bit_if_single_stream_entry_doesnt_exist_drops_entry_and_carries_on()
        {
            _ptableVersion = PTableVersions.IndexV2;
        }

        [OneTimeSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();

            _indexDir = PathName;
            var fakeReader = new TFReaderLease(new FakeIndexReader2());
            _lowHasher = new XXHashUnsafe();
            _highHasher = new Murmur3AUnsafe();
            _tableIndex = new TableIndex(_indexDir, _lowHasher, _highHasher,
                () => new HashListMemTable(PTableVersions.IndexV1, maxSize: 2),
                () => fakeReader,
                PTableVersions.IndexV1,
                maxSizeForMemory: 5,
                maxTablesPerLevel: 2);
            _tableIndex.Initialize(long.MaxValue);

            _tableIndex.Add(1, "LPN-FC002_LPK51001", 0, 1);
            _tableIndex.Add(1, "account--696193173", 0, 2);

            _tableIndex.Close(false);

            _tableIndex = new TableIndex(_indexDir, _lowHasher, _highHasher,
                () => new HashListMemTable(_ptableVersion, maxSize: 2),
                () => fakeReader,
                _ptableVersion,
                maxSizeForMemory: 5,
                maxTablesPerLevel: 2);
            _tableIndex.Initialize(long.MaxValue);

            _tableIndex.Add(1, "account--696193173", 1, 3);
            _tableIndex.Add(1, "LPN-FC002_LPK51001", 1, 4);

            Thread.Sleep(500);
        }

        [OneTimeTearDown]
        public override void TestFixtureTearDown()
        {
            _tableIndex.Close();

            base.TestFixtureTearDown();
        }
        
        [Test]
        public void should_have_all_entries_except_scavenged()
        {
            var streamId = "LPN-FC002_LPK51001";
            var result = _tableIndex.GetRange(streamId, 0, 1).ToArray();
            var hash = (ulong)_lowHasher.Hash(streamId) << 32 | _highHasher.Hash(streamId);

            Assert.That(result.Count(), Is.EqualTo(1));

            Assert.That(result[0].Stream, Is.EqualTo(hash));

            Assert.That(result[0].Stream, Is.EqualTo(hash));
            Assert.That(result[0].Version, Is.EqualTo(1));
            Assert.That(result[0].Position, Is.EqualTo(4));

            streamId = "account--696193173";
            result = _tableIndex.GetRange(streamId, 0, 1).ToArray();
            hash = (ulong)_lowHasher.Hash(streamId) << 32 | _highHasher.Hash(streamId);

            Assert.That(result.Count(), Is.EqualTo(2));

            Assert.That(result[0].Stream, Is.EqualTo(hash));
            Assert.That(result[0].Version, Is.EqualTo(1));
            Assert.That(result[0].Position, Is.EqualTo(3));

            Assert.That(result[1].Stream, Is.EqualTo(hash));
            Assert.That(result[1].Version, Is.EqualTo(0));
            Assert.That(result[1].Position, Is.EqualTo(2));
        }

        private class FakeIndexReader2 : ITransactionFileReader
        {
            public void Reposition(long position)
            {
                throw new NotImplementedException();
            }

            public SeqReadResult TryReadNext()
            {
                throw new NotImplementedException();
            }

            public SeqReadResult TryReadPrev()
            {
                throw new NotImplementedException();
            }

            public RecordReadResult TryReadAt(long position)
            {
                var eventStreamId = position % 2 == 0 ? "account--696193173" : "LPN-FC002_LPK51001";
                var record = (LogRecord)new PrepareLogRecord(position, Guid.NewGuid(), Guid.NewGuid(), 0, 0, eventStreamId, -1, DateTime.UtcNow, PrepareFlags.None, "type", new byte[0], null);
                return new RecordReadResult(true, position + 1, record, 1);
            }

            public bool ExistsAt(long position)
            {
                return position != 1;
            }
        }
    }
}