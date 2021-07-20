using System;
using EventStore.Core.Index;
using EventStore.Core.Index.Hashes;
using EventStore.Core.LogV2;
using EventStore.Core.Tests.Services.Storage;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.LogRecords;
using Xunit;

namespace EventStore.Core.XUnit.Tests.LogV2 {
	public class LogV2StreamExistenceFilterInitializerTests : DirectoryPerTest<LogV2StreamExistenceFilterInitializerTests> {
		private readonly TableIndex<string> _tableIndex;
		private readonly LogV2StreamExistenceFilterInitializer _sut;
		private readonly FakeInMemoryTfReader _log;
		private readonly MockExistenceFilter _filter;
		private readonly int _recordOffset = 100;
		private long _nextLogPosition = 0;

		public LogV2StreamExistenceFilterInitializerTests() {
			_log = new FakeInMemoryTfReader(recordOffset: _recordOffset);
			_tableIndex = new TableIndex<string>(
				directory: Fixture.Directory,
				lowHasher: new XXHashUnsafe(),
				highHasher: new Murmur3AUnsafe(),
				emptyStreamId: string.Empty,
				memTableFactory: () => new HashListMemTable(
					version: PTableVersions.IndexV4,
					maxSize: 1_000_000 * 2),
				tfReaderFactory: () => new TFReaderLease(_log),
				ptableVersion: PTableVersions.IndexV4,
				maxAutoMergeIndexLevel: int.MaxValue,
				pTableMaxReaderCount: 5);
			_tableIndex.Initialize(0);

			_sut = new LogV2StreamExistenceFilterInitializer(
				tfReaderFactory: () => new TFReaderLease(_log),
				tableIndex: _tableIndex);
			var hasher = new CompositeHasher<string>(new XXHashUnsafe(), new Murmur3AUnsafe());
			_filter = new MockExistenceFilter(hasher);

		}

		private void AddEventToSut(string stream, int eventNumber) {
			var record = LogRecord.SingleWrite(
				factory: new LogV2RecordFactory(),
				logPosition: _nextLogPosition,
				correlationId: Guid.NewGuid(),
				eventId: Guid.NewGuid(),
				eventStreamId: stream,
				expectedVersion: eventNumber - 1,
				eventType: "eventType",
				data: ReadOnlyMemory<byte>.Empty,
				metadata: ReadOnlyMemory<byte>.Empty);

			_log.AddRecord(record, _nextLogPosition);
			_tableIndex.Add(
				commitPos: _nextLogPosition,
				streamId: stream,
				version: record.ExpectedVersion + 1,
				position: _nextLogPosition);

			_nextLogPosition += _recordOffset;
		}

		[Fact]
		public void can_initialize_empty() {
			Assert.Equal(-1, _filter.CurrentCheckpoint);
			_sut.Initialize(_filter);
			Assert.Equal(-1, _filter.CurrentCheckpoint);
			Assert.Empty(_filter.Hashes);
		}

		[Fact]
		public void can_initialize_from_beginning() {
			// (implementation detail: initializes from index)
			AddEventToSut("1", 0);
			AddEventToSut("1", 1);
			AddEventToSut("2", 0);
			AddEventToSut("2", 1);
			AddEventToSut("3", 0);
			AddEventToSut("3", 1);

			_sut.Initialize(_filter);

			Assert.Equal(600, _filter.CurrentCheckpoint);
			Assert.Equal(3, _filter.Hashes.Count);
		}

		[Fact]
		public void can_initialize_incremental() {
			// (implementation detail: initializes from log)
			_filter.CurrentCheckpoint = 0;

			AddEventToSut("1", 0);
			AddEventToSut("1", 1);
			AddEventToSut("2", 0);
			AddEventToSut("2", 1);
			AddEventToSut("3", 0);
			AddEventToSut("3", 1);

			_sut.Initialize(_filter);

			Assert.Equal(600, _filter.CurrentCheckpoint);
			Assert.Equal(3, _filter.Hashes.Count);
		}

		[Fact]
		public void cannot_initialize_with_v1_indexes() {
			var tableIndex = new TableIndex<string>(
				directory: Fixture.Directory,
				lowHasher: new XXHashUnsafe(),
				highHasher: new Murmur3AUnsafe(),
				emptyStreamId: string.Empty,
				memTableFactory: () => new HashListMemTable(
					version: PTableVersions.IndexV1,
					maxSize: 1_000_000 * 2),
				tfReaderFactory: () => throw new Exception("index tried to read the log"),
				ptableVersion: PTableVersions.IndexV1,
				maxAutoMergeIndexLevel: int.MaxValue,
				pTableMaxReaderCount: 5);
			tableIndex.Initialize(0);

			var sut = new LogV2StreamExistenceFilterInitializer(
				tfReaderFactory: () => throw new Exception("initializer tried to read the log"),
				tableIndex: tableIndex);

			var filter = new MockExistenceFilter(hasher: null);

			var ex = Assert.Throws<NotSupportedException>(() => {
				sut.Initialize(filter);
			});
			Assert.Equal(
				"The Stream Existence Filter is not supported with V1 index files. " +
				"Please disable the filter by setting StreamExistenceFilterSize to 0, or rebuild the indexes.",
				ex.Message);
		}
	}
}
