using System.Linq;
using System.Threading;
using EventStore.Core.Index;
using EventStore.Core.TransactionLog;
using NUnit.Framework;
using EventStore.Core.Index.Hashes;
using System;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Tests.Index.IndexV3 {
	[TestFixture, Category("LongRunning")]
	public class when_upgrading_index_to_64bit_stream_version : SpecificationWithDirectoryPerTestFixture {
		private TableIndex _tableIndex;
		private IHasher _lowHasher;
		private IHasher _highHasher;
		private string _indexDir;
		protected byte _ptableVersion;

		public when_upgrading_index_to_64bit_stream_version() {
			_ptableVersion = PTableVersions.IndexV3;
		}

		[OneTimeSetUp]
		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();

			_indexDir = PathName;
			var fakeReader = new TFReaderLease(new FakeIndexReader());
			_lowHasher = new XXHashUnsafe();
			_highHasher = new Murmur3AUnsafe();
			_tableIndex = new TableIndex(_indexDir, _lowHasher, _highHasher,
				() => new HashListMemTable(PTableVersions.IndexV2, maxSize: 5),
				() => fakeReader,
				PTableVersions.IndexV2,
				5,
				maxSizeForMemory: 5,
				maxTablesPerLevel: 2);
			_tableIndex.Initialize(long.MaxValue);

			_tableIndex.Add(1, "testStream-1", 0, 1);
			_tableIndex.Add(1, "testStream-2", 0, 2);
			_tableIndex.Add(1, "testStream-1", 1, 3);
			_tableIndex.Add(1, "testStream-2", 1, 4);
			_tableIndex.Add(1, "testStream-1", 2, 5);

			_tableIndex.Close(false);

			_tableIndex = new TableIndex(_indexDir, _lowHasher, _highHasher,
				() => new HashListMemTable(_ptableVersion, maxSize: 5),
				() => fakeReader,
				_ptableVersion,
				5,
				maxSizeForMemory: 5,
				maxTablesPerLevel: 2);
			_tableIndex.Initialize(long.MaxValue);

			_tableIndex.Add(1, "testStream-2", 2, 6);
			_tableIndex.Add(1, "testStream-1", 3, 7);
			_tableIndex.Add(1, "testStream-2", 3, 8);
			_tableIndex.Add(1, "testStream-1", 4, 9);
			_tableIndex.Add(1, "testStream-2", 4, 10);

			Thread.Sleep(500);
		}

		[OneTimeTearDown]
		public override void TestFixtureTearDown() {
			_tableIndex.Close();

			base.TestFixtureTearDown();
		}

		[Test]
		public void should_have_entries_in_sorted_order() {
			var streamId = "testStream-2";
			var result = _tableIndex.GetRange(streamId, 0, 4).ToArray();
			var hash = (ulong)_lowHasher.Hash(streamId) << 32 | _highHasher.Hash(streamId);

			Assert.That(result.Count(), Is.EqualTo(5));

			Assert.That(result[0].Stream, Is.EqualTo(hash));
			Assert.That(result[0].Version, Is.EqualTo(4));
			Assert.That(result[0].Position, Is.EqualTo(10));

			Assert.That(result[1].Stream, Is.EqualTo(hash));
			Assert.That(result[1].Version, Is.EqualTo(3));
			Assert.That(result[1].Position, Is.EqualTo(8));

			Assert.That(result[2].Stream, Is.EqualTo(hash));
			Assert.That(result[2].Version, Is.EqualTo(2));
			Assert.That(result[2].Position, Is.EqualTo(6));

			Assert.That(result[3].Stream, Is.EqualTo(hash));
			Assert.That(result[3].Version, Is.EqualTo(1));
			Assert.That(result[3].Position, Is.EqualTo(4));

			Assert.That(result[4].Stream, Is.EqualTo(hash));
			Assert.That(result[4].Version, Is.EqualTo(0));
			Assert.That(result[4].Position, Is.EqualTo(2));

			streamId = "testStream-1";
			result = _tableIndex.GetRange(streamId, 0, 4).ToArray();
			hash = (ulong)_lowHasher.Hash(streamId) << 32 | _highHasher.Hash(streamId);

			Assert.That(result.Count(), Is.EqualTo(5));

			Assert.That(result[0].Stream, Is.EqualTo(hash));
			Assert.That(result[0].Version, Is.EqualTo(4));
			Assert.That(result[0].Position, Is.EqualTo(9));

			Assert.That(result[1].Stream, Is.EqualTo(hash));
			Assert.That(result[1].Version, Is.EqualTo(3));
			Assert.That(result[1].Position, Is.EqualTo(7));

			Assert.That(result[2].Stream, Is.EqualTo(hash));
			Assert.That(result[2].Version, Is.EqualTo(2));
			Assert.That(result[2].Position, Is.EqualTo(5));

			Assert.That(result[3].Stream, Is.EqualTo(hash));
			Assert.That(result[3].Version, Is.EqualTo(1));
			Assert.That(result[3].Position, Is.EqualTo(3));

			Assert.That(result[4].Stream, Is.EqualTo(hash));
			Assert.That(result[4].Version, Is.EqualTo(0));
			Assert.That(result[4].Position, Is.EqualTo(1));
		}
	}

	public class FakeIndexReader : ITransactionFileReader {
		public void Reposition(long position) {
			throw new NotImplementedException();
		}

		public SeqReadResult TryReadNext() {
			throw new NotImplementedException();
		}

		public SeqReadResult TryReadPrev() {
			throw new NotImplementedException();
		}

		public RecordReadResult TryReadAt(long position) {
			var record = (LogRecord)new PrepareLogRecord(position, Guid.NewGuid(), Guid.NewGuid(), 0, 0,
				position % 2 == 0 ? "testStream-2" : "testStream-1", -1, DateTime.UtcNow, PrepareFlags.None, "type",
				new byte[0], null);
			return new RecordReadResult(true, position + 1, record, 1);
		}

		public bool ExistsAt(long position) {
			return true;
		}
	}
}
