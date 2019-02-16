using System;
using EventStore.ClientAPI;
using EventStore.Core.Tests;
using NUnit.Framework;
using EventStore.Core.Index;
using EventStore.Core.Index.Hashes;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.Tests.Services.Storage.HashCollisions {
	[TestFixture]
	public class HashCollisionTestFixture : SpecificationWithDirectoryPerTestFixture {
		protected int _hashCollisionReadLimit = 5;
		protected int _maxMemTableSize = 5;
		protected TableIndex _tableIndex;
		protected IIndexReader _indexReader;
		protected IIndexBackend _indexBackend;
		protected IHasher _lowHasher;
		protected IHasher _highHasher;
		protected string _indexDir;
		protected TFReaderLease _fakeReader;

		protected virtual void given() {
		}

		protected virtual void when() {
		}

		[OneTimeSetUp]
		public void Setup() {
			given();
			_indexDir = PathName;
			_fakeReader = new TFReaderLease(new FakeReader());
			_indexBackend = new FakeIndexBackend(_fakeReader);
			_lowHasher = new XXHashUnsafe();
			_highHasher = new Murmur3AUnsafe();
			_tableIndex = new TableIndex(_indexDir, _lowHasher, _highHasher,
				() => new HashListMemTable(PTableVersions.IndexV1, maxSize: _maxMemTableSize),
				() => _fakeReader,
				PTableVersions.IndexV1,
				5,
				maxSizeForMemory: _maxMemTableSize,
				maxTablesPerLevel: 2);
			_tableIndex.Initialize(long.MaxValue);
			_indexReader = new IndexReader(_indexBackend, _tableIndex, new EventStore.Core.Data.StreamMetadata(),
				_hashCollisionReadLimit, skipIndexScanOnRead: false);

			when();
			//wait for the mem table to be dumped
			System.Threading.Thread.Sleep(500);
		}

		public override void TestFixtureTearDown() {
			_tableIndex.Close();
			base.TestFixtureTearDown();
		}
	}

	[TestFixture]
	public class when_stream_does_not_exist : HashCollisionTestFixture {
		protected override void given() {
			_hashCollisionReadLimit = 5;
		}

		protected override void when() {
			//mem table
			_tableIndex.Add(1, "LPN-FC002_LPK51001", 0, 3);
			_tableIndex.Add(1, "LPN-FC002_LPK51001", 1, 5);
		}

		[Test]
		public void should_return_no_stream() {
			Assert.AreEqual(ExpectedVersion.NoStream, _indexReader.GetStreamLastEventNumber("account--696193173"));
		}
	}

	[TestFixture]
	public class when_stream_is_out_of_range_of_read_limit : HashCollisionTestFixture {
		protected override void given() {
			_hashCollisionReadLimit = 1;
		}

		protected override void when() {
			//ptable 1
			_tableIndex.Add(1, "account--696193173", 0, 0);
			_tableIndex.Add(1, "LPN-FC002_LPK51001", 0, 3);
			_tableIndex.Add(1, "LPN-FC002_LPK51001", 1, 5);
			_tableIndex.Add(1, "LPN-FC002_LPK51001", 2, 7);
			_tableIndex.Add(1, "LPN-FC002_LPK51001", 3, 9);
			//mem table
			_tableIndex.Add(1, "LPN-FC002_LPK51001", 4, 13);
		}

		[Test]
		public void should_return_invalid_event_number() {
			Assert.AreEqual(EventStore.Core.Data.EventNumber.Invalid,
				_indexReader.GetStreamLastEventNumber("account--696193173"));
		}
	}

	[TestFixture]
	public class when_stream_is_in_of_range_of_read_limit : HashCollisionTestFixture {
		protected override void given() {
			_hashCollisionReadLimit = 5;
		}

		protected override void when() {
			//ptable 1
			_tableIndex.Add(1, "account--696193173", 0, 0);
			_tableIndex.Add(1, "LPN-FC002_LPK51001", 0, 3);
			_tableIndex.Add(1, "LPN-FC002_LPK51001", 1, 5);
			_tableIndex.Add(1, "LPN-FC002_LPK51001", 2, 7);
			_tableIndex.Add(1, "LPN-FC002_LPK51001", 3, 9);
			//mem table
			_tableIndex.Add(1, "LPN-FC002_LPK51001", 4, 13);
		}

		[Test]
		public void should_return_last_event_number() {
			Assert.AreEqual(0, _indexReader.GetStreamLastEventNumber("account--696193173"));
		}
	}

	[TestFixture]
	public class when_hash_read_limit_is_not_reached : HashCollisionTestFixture {
		protected override void given() {
			_hashCollisionReadLimit = 3;
		}

		protected override void when() {
			//ptable 1
			_tableIndex.Add(1, "account--696193173", 0, 0);
			_tableIndex.Add(1, "LPN-FC002_LPK51001", 0, 3);
			_tableIndex.Add(1, "LPN-FC002_LPK51001", 1, 5);
			_tableIndex.Add(1, "LPN-FC002_LPK51001", 2, 7);
			_tableIndex.Add(1, "LPN-FC002_LPK51001", 3, 9);
		}

		[Test]
		public void should_return_invalid_event_number() {
			Assert.AreEqual(EventStore.Core.Data.EventNumber.Invalid,
				_indexReader.GetStreamLastEventNumber("account--696193173"));
		}
	}

	[TestFixture]
	public class when_index_contains_duplicate_entries : HashCollisionTestFixture {
		private string streamId = "account--696193173";

		protected override void given() {
			_hashCollisionReadLimit = 5;
		}

		protected override void when() {
			//ptable 1
			_tableIndex.Add(1, streamId, 0, 2);
			_tableIndex.Add(1, streamId, 0, 4);
			_tableIndex.Add(1, streamId, 1, 6);
			_tableIndex.Add(1, streamId, 2, 8);
		}

		[Test]
		public void should_be_able_to_read_stream_events_forward_and_exclude_duplicates() {
			var result = _indexReader.ReadStreamEventsForward(streamId, 0, int.MaxValue);
			Assert.AreEqual(3, result.Records.Length);

			Assert.AreEqual(streamId, result.Records[0].EventStreamId);
			Assert.AreEqual(0, result.Records[0].EventNumber);
			Assert.AreEqual(2, result.Records[0].LogPosition);

			Assert.AreEqual(streamId, result.Records[1].EventStreamId);
			Assert.AreEqual(1, result.Records[1].EventNumber);
			Assert.AreEqual(6, result.Records[1].LogPosition);

			Assert.AreEqual(streamId, result.Records[2].EventStreamId);
			Assert.AreEqual(2, result.Records[2].EventNumber);
			Assert.AreEqual(8, result.Records[2].LogPosition);
		}

		[Test]
		public void should_be_able_to_read_stream_events_backward_and_exclude_duplicates() {
			var result = _indexReader.ReadStreamEventsBackward(streamId, 2, int.MaxValue);
			Assert.AreEqual(3, result.Records.Length);

			Assert.AreEqual(streamId, result.Records[2].EventStreamId);
			Assert.AreEqual(0, result.Records[2].EventNumber);
			Assert.AreEqual(2, result.Records[2].LogPosition);

			Assert.AreEqual(streamId, result.Records[1].EventStreamId);
			Assert.AreEqual(1, result.Records[1].EventNumber);
			Assert.AreEqual(6, result.Records[1].LogPosition);

			Assert.AreEqual(streamId, result.Records[0].EventStreamId);
			Assert.AreEqual(2, result.Records[0].EventNumber);
			Assert.AreEqual(8, result.Records[0].LogPosition);
		}

		[Test]
		public void should_be_able_to_read_single_event_and_exclude_duplicates() {
			var result = _indexReader.ReadEvent(streamId, 0);

			Assert.AreEqual(streamId, result.Record.EventStreamId);
			Assert.AreEqual(0, result.Record.EventNumber);
			Assert.AreEqual(2, result.Record.LogPosition);
		}
	}

	[TestFixture]
	public class
		when_index_contains_duplicate_entries_and_the_duplicate_is_a_64bit_index_entry : HashCollisionTestFixture {
		private string streamId = "account--696193173";

		protected override void given() {
			_maxMemTableSize = 3;
			_hashCollisionReadLimit = 5;
		}

		protected override void when() {
			//ptable 1 with 32bit indexes
			_tableIndex.Add(1, streamId, 0, 2);
			_tableIndex.Add(1, streamId, 1, 4);
			_tableIndex.Add(1, streamId, 2, 6);
			System.Threading.Thread.Sleep(500);
			_tableIndex.Close(false);
			_tableIndex = new TableIndex(_indexDir, _lowHasher, _highHasher,
				() => new HashListMemTable(PTableVersions.IndexV2, maxSize: _maxMemTableSize),
				() => _fakeReader,
				PTableVersions.IndexV2,
				5,
				maxSizeForMemory: _maxMemTableSize,
				maxTablesPerLevel: 2);
			_tableIndex.Initialize(long.MaxValue);
			_indexReader = new IndexReader(_indexBackend, _tableIndex, new EventStore.Core.Data.StreamMetadata(),
				_hashCollisionReadLimit, skipIndexScanOnRead: false);
			//memtable with 64bit indexes
			_tableIndex.Add(1, streamId, 0, 8);
		}

		[Test]
		public void should_return_the_correct_last_event_number() {
			var result = _indexReader.GetStreamLastEventNumber(streamId);
			Assert.AreEqual(2, result);
		}

		[Test]
		public void should_be_able_to_read_stream_events_forward_and_exclude_duplicates() {
			var result = _indexReader.ReadStreamEventsForward(streamId, 0, int.MaxValue);
			Assert.AreEqual(3, result.Records.Length);

			Assert.AreEqual(streamId, result.Records[0].EventStreamId);
			Assert.AreEqual(0, result.Records[0].EventNumber);
			Assert.AreEqual(2, result.Records[0].LogPosition);

			Assert.AreEqual(streamId, result.Records[1].EventStreamId);
			Assert.AreEqual(1, result.Records[1].EventNumber);
			Assert.AreEqual(4, result.Records[1].LogPosition);

			Assert.AreEqual(streamId, result.Records[2].EventStreamId);
			Assert.AreEqual(2, result.Records[2].EventNumber);
			Assert.AreEqual(6, result.Records[2].LogPosition);
		}

		[Test]
		public void should_be_able_to_read_stream_events_backward_and_exclude_duplicates() {
			var result = _indexReader.ReadStreamEventsBackward(streamId, 2, int.MaxValue);
			Assert.AreEqual(3, result.Records.Length);

			Assert.AreEqual(streamId, result.Records[2].EventStreamId);
			Assert.AreEqual(0, result.Records[2].EventNumber);
			Assert.AreEqual(2, result.Records[2].LogPosition);

			Assert.AreEqual(streamId, result.Records[1].EventStreamId);
			Assert.AreEqual(1, result.Records[1].EventNumber);
			Assert.AreEqual(4, result.Records[1].LogPosition);

			Assert.AreEqual(streamId, result.Records[0].EventStreamId);
			Assert.AreEqual(2, result.Records[0].EventNumber);
			Assert.AreEqual(6, result.Records[0].LogPosition);
		}

		[Test]
		public void should_be_able_to_read_single_event_and_exclude_duplicates() {
			var result = _indexReader.ReadEvent(streamId, 0);

			Assert.AreEqual(streamId, result.Record.EventStreamId);
			Assert.AreEqual(0, result.Record.EventNumber);
			Assert.AreEqual(2, result.Record.LogPosition);
		}
	}

	public class FakeIndexBackend : IIndexBackend {
		private TFReaderLease _readerLease;

		public FakeIndexBackend(TFReaderLease readerLease) {
			_readerLease = readerLease;
		}

		public TFReaderLease BorrowReader() {
			return _readerLease;
		}

		public IndexBackend.EventNumberCached TryGetStreamLastEventNumber(string streamId) {
			return new IndexBackend.EventNumberCached(-1, null); //always return uncached
		}

		public IndexBackend.MetadataCached TryGetStreamMetadata(string streamId) {
			return new IndexBackend.MetadataCached();
		}

		public long? UpdateStreamLastEventNumber(int cacheVersion, string streamId, long? lastEventNumber) {
			return null;
		}

		public EventStore.Core.Data.StreamMetadata UpdateStreamMetadata(int cacheVersion, string streamId,
			EventStore.Core.Data.StreamMetadata metadata) {
			return null;
		}

		public long? SetStreamLastEventNumber(string streamId, long lastEventNumber) {
			return null;
		}

		public EventStore.Core.Data.StreamMetadata SetStreamMetadata(string streamId,
			EventStore.Core.Data.StreamMetadata metadata) {
			return null;
		}

		public void SetSystemSettings(EventStore.Core.Data.SystemSettings systemSettings) {
		}

		public EventStore.Core.Data.SystemSettings GetSystemSettings() {
			return null;
		}
	}

	public class FakeReader : ITransactionFileReader {
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
				position % 2 == 0 ? "account--696193173" : "LPN-FC002_LPK51001", -1, DateTime.UtcNow, PrepareFlags.None,
				"type", new byte[0], null);
			return new RecordReadResult(true, position + 1, record, 1);
		}

		public bool ExistsAt(long position) {
			return true;
		}
	}
}
