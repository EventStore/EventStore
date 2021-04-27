using System;
using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using Xunit;

namespace EventStore.Core.Tests.XUnit.LogAbstraction {
	// check that lookups of the various combinations of virtual/normal/meta
	// work in both directions and in the stream index.
	public class LogFormatAbstractorV3Tests {
		readonly LogFormatAbstractor<long> _sut = LogFormatAbstractor.CreateV3();
		readonly string _stream = "account-abc";
		readonly string _systemStream = "$something-parked";
		readonly long _streamId;
		readonly long _systemStreamId;
		readonly MockIndexReader _mockIndexReader = new();
		readonly int _numStreams;

		public LogFormatAbstractorV3Tests() {
			_sut.StreamNamesProvider.SetReader(_mockIndexReader);
			GetOrAddId(_stream, out _streamId, out _, out _);
			GetOrAddId(_systemStream, out _systemStreamId, out _, out _);
			_numStreams = 2;
		}

		// it is up to the user of the V3 abstractor to index created streams, simulate that here.
		// this is because we are currently using the normal event index to look up the stream names
		bool GetOrAddId(string streamName, out long streamId, out long createdId, out string createdName) {
			_sut.StreamNameIndex.GetOrAddId(
				recordFactory: _sut.RecordFactory,
				streamName: streamName,
				logPosition: 123,
				streamId: out streamId,
				streamRecord: out var record);

			if (record is LogV3StreamRecord streamRecord) {
				createdId = streamRecord.Record.SubHeader.ReferenceId;
				createdName = streamRecord.StreamName;
				_mockIndexReader.Add(streamRecord.ExpectedVersion + 1, streamRecord);
				return false;
			} else {
				createdId = default;
				createdName = default;
				return true;
			}
		}

		[Fact]
		public void can_add_another_stream() {
			Assert.False(GetOrAddId("new-stream-1", out var newStreamId1, out var createdId1, out var createdName1));
			Assert.False(GetOrAddId("new-stream-2", out var newStreamId2, out var createdId2, out var createdName2));
			Assert.Equal(newStreamId1 + 2, newStreamId2);
			Assert.Equal(newStreamId1, createdId1);
			Assert.Equal(newStreamId2, createdId2);
			Assert.Equal("new-stream-1", createdName1);
			Assert.Equal("new-stream-2", createdName2);
			Assert.Equal(_numStreams + 2, _mockIndexReader.Count);
		}

		[Fact]
		public void can_add_another_meta_stream() {
			Assert.False(GetOrAddId("$$new-stream-3", out var newStreamId1, out var createdId1, out var createdName1));
			Assert.False(GetOrAddId("$$new-stream-4", out var newStreamId2, out var createdId2, out var createdName2));
			Assert.Equal(_numStreams + 2, _mockIndexReader.Count);

			// ids should be 2 apart to leave room for meta
			Assert.Equal(newStreamId1 + 2, newStreamId2);

			// the stream actually created is not the meta but the main counterpart of it
			Assert.Equal("new-stream-3", createdName1);
			Assert.Equal("new-stream-4", createdName2);
			Assert.Equal(newStreamId1 - 1, createdId1);
			Assert.Equal(newStreamId2 - 1, createdId2);
		}

		[Fact]
		public void can_add_another_system_stream() {
			Assert.False(GetOrAddId("$new-stream-5", out var newStreamId1, out var createdId1, out var createdName1));
			Assert.False(GetOrAddId("$new-stream-6", out var newStreamId2, out var createdId2, out var createdName2));
			Assert.Equal(_numStreams + 2, _mockIndexReader.Count);

			// ids should be 2 apart to leave room for meta
			Assert.Equal(newStreamId1 + 2, newStreamId2);

			// the stream actually created is the system stream
			Assert.Equal("$new-stream-5", createdName1);
			Assert.Equal("$new-stream-6", createdName2);
			Assert.Equal(newStreamId1, createdId1);
			Assert.Equal(newStreamId2, createdId2);
		}

		[Fact]
		public void can_add_another_system_meta_stream() {
			Assert.False(GetOrAddId("$$$new-stream-7", out var newStreamId1, out var createdId1, out var createdName1));
			Assert.False(GetOrAddId("$$$new-stream-8", out var newStreamId2, out var createdId2, out var createdName2));
			Assert.Equal(_numStreams + 2, _mockIndexReader.Count);

			// ids should be 2 apart to leave room for meta
			Assert.Equal(newStreamId1 + 2, newStreamId2);

			// the stream actually created is not the meta but the main counterpart of it
			Assert.Equal("$new-stream-7", createdName1);
			Assert.Equal("$new-stream-8", createdName2);
			Assert.Equal(newStreamId1 - 1, createdId1);
			Assert.Equal(newStreamId2 - 1, createdId2);
		}

		[Fact]
		public void can_find_existing_stream() {
			Assert.True(GetOrAddId(_stream, out var streamId, out _, out _));
			Assert.Equal(_numStreams, _mockIndexReader.Count);
			Assert.Equal(_streamId, streamId);
			Assert.Equal(_streamId, _sut.StreamIds.LookupId(_stream));
			Assert.Equal(_stream, _sut.StreamNames.LookupName(_streamId));
			Assert.False(_sut.SystemStreams.IsMetaStream(streamId));
			Assert.False(_sut.SystemStreams.IsSystemStream(streamId));
		}

		[Fact]
		public void can_find_existing_meta_stream() {
			var metaStreamName = "$$" + _stream;
			var expectedMetaStreamId = _streamId + 1;
			Assert.True(GetOrAddId(metaStreamName, out var streamId, out _, out _));
			Assert.Equal(_numStreams, _mockIndexReader.Count);
			Assert.Equal(expectedMetaStreamId, streamId);
			Assert.Equal(expectedMetaStreamId, _sut.StreamIds.LookupId(metaStreamName));
			Assert.Equal(metaStreamName, _sut.StreamNames.LookupName(expectedMetaStreamId));
			Assert.True(_sut.SystemStreams.IsMetaStream(streamId));
			Assert.True(_sut.SystemStreams.IsSystemStream(streamId));
		}

		[Fact]
		public void can_find_existing_system_stream() {
			Assert.True(GetOrAddId(_systemStream, out var streamId, out _, out _));
			Assert.Equal(_numStreams, _mockIndexReader.Count);
			Assert.Equal(_systemStreamId, streamId);
			Assert.Equal(_systemStreamId, _sut.StreamIds.LookupId(_systemStream));
			Assert.Equal(_systemStream, _sut.StreamNames.LookupName(_systemStreamId));
			Assert.False(_sut.SystemStreams.IsMetaStream(streamId));
			Assert.True(_sut.SystemStreams.IsSystemStream(streamId));
		}

		[Fact]
		public void can_find_existing_system_meta_stream() {
			var metaStreamName = "$$" + _systemStream;
			var expectedMetaStreamId = _systemStreamId + 1;
			Assert.True(GetOrAddId(metaStreamName, out var streamId, out _, out _));
			Assert.Equal(_numStreams, _mockIndexReader.Count);
			Assert.Equal(expectedMetaStreamId, streamId);
			Assert.Equal(expectedMetaStreamId, _sut.StreamIds.LookupId(metaStreamName));
			Assert.Equal(metaStreamName, _sut.StreamNames.LookupName(expectedMetaStreamId));
			Assert.True(_sut.SystemStreams.IsMetaStream(streamId));
			Assert.True(_sut.SystemStreams.IsSystemStream(streamId));
		}

		[Theory]
		[InlineData(2, "$all")]
		[InlineData(4, "$streams-created")]
		[InlineData(6, "$settings")]
		public void can_find_virtual_stream(long expectedId, string name) {
			Assert.True(GetOrAddId(name, out var streamId, out _, out _));
			Assert.Equal(_numStreams, _mockIndexReader.Count);
			Assert.Equal(expectedId, streamId);
			Assert.Equal(expectedId, _sut.StreamIds.LookupId(name));
			Assert.Equal(name, _sut.StreamNames.LookupName(expectedId));
			Assert.False(_sut.SystemStreams.IsMetaStream(streamId));
			Assert.True(_sut.SystemStreams.IsSystemStream(streamId));
		}

		[Fact]
		public void can_find_virtual_meta_stream() {
			Assert.True(GetOrAddId("$$$all", out var streamId, out _, out _));
			Assert.Equal(_numStreams, _mockIndexReader.Count);
			Assert.Equal(3, streamId);
			Assert.Equal(3, _sut.StreamIds.LookupId("$$$all"));
			Assert.Equal("$$$all", _sut.StreamNames.LookupName(3));
			Assert.True(_sut.SystemStreams.IsMetaStream(streamId));
			Assert.True(_sut.SystemStreams.IsSystemStream(streamId));
		}

		class MockIndexReader : IIndexReader<long> {
			private readonly Dictionary<long, IPrepareLogRecord<long>> _streamsStream = new();

			public void Add(long streamId, IPrepareLogRecord<long> streamRecord) => _streamsStream.Add(streamId, streamRecord);

			public int Count => _streamsStream.Count;

			public IPrepareLogRecord<long> ReadPrepare(long streamId, long eventNumber) {
				// simulates what would be in the index.
				return _streamsStream[eventNumber];
			}

			public long CachedStreamInfo => throw new NotImplementedException();

			public long NotCachedStreamInfo => throw new NotImplementedException();

			public long HashCollisions => throw new NotImplementedException();

			public StorageMessage.EffectiveAcl GetEffectiveAcl(long streamId) =>
				throw new NotImplementedException();

			public long GetEventStreamIdByTransactionId(long transactionId) =>
				throw new NotImplementedException();

			public long GetStreamLastEventNumber(long streamId) =>
				throw new NotImplementedException();

			public StreamMetadata GetStreamMetadata(long streamId) =>
				throw new NotImplementedException();

			public IndexReadEventResult ReadEvent(string streamName, long streamId, long eventNumber) =>
				throw new NotImplementedException();

			public IndexReadStreamResult ReadStreamEventsBackward(string streamName, long streamId, long fromEventNumber, int maxCount) =>
				throw new NotImplementedException();

			public IndexReadStreamResult ReadStreamEventsForward(string streamName, long streamId, long fromEventNumber, int maxCount) =>
				throw new NotImplementedException();
		}
	}
}
