using System;
using System.Collections.Generic;
using System.IO;
using EventStore.Core.Data;
using EventStore.Core.LogAbstraction;
using EventStore.Core.LogV3;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;
using Xunit;
using StreamId = System.UInt32;
using EventTypeId = System.UInt32;

namespace EventStore.Core.XUnit.Tests.LogAbstraction {
	// check that lookups of the various combinations of virtual/normal/meta
	// work in both directions and in the stream index.
	public class LogFormatAbstractorV3Tests : IDisposable {
		readonly static string _outputDir = $"testoutput/{nameof(LogFormatAbstractorV3Tests)}";
		readonly LogFormatAbstractor<StreamId> _sut = new LogV3FormatAbstractorFactory().Create(new() {
			IndexDirectory = _outputDir,
			InMemory = false,
			StreamExistenceFilterSize = 1_000_000,
			StreamExistenceFilterCheckpoint = new InMemoryCheckpoint(),
		});

		readonly string _stream = "account-abc";
		readonly string _systemStream = "$something-parked";
		readonly StreamId _streamId;
		readonly StreamId _systemStreamId;
		readonly string _eventType = "some-event";
		readonly EventTypeId _eventTypeId;
		readonly MockIndexReader _mockIndexReader = new();
		readonly int _numStreams;
		readonly int _numEventTypes;

		public LogFormatAbstractorV3Tests() {
			TryDeleteDirectory();
			_sut.StreamNamesProvider.SetReader(_mockIndexReader);
			_sut.StreamExistenceFilter.Initialize(_sut.StreamExistenceFilterInitializer, 0);
			Assert.False(GetOrReserve(_stream, out _streamId, out _, out _));
			Assert.False(GetOrReserve(_systemStream, out _systemStreamId, out _, out _));
			Assert.False(GetOrReserveEventType(_eventType, out _eventTypeId, out _, out _));
			
			_numStreams = 2;
			_numEventTypes = 1;
		}

		public void Dispose() {
			_sut.Dispose();
			TryDeleteDirectory();
		}

		void TryDeleteDirectory() {
			try {
				Directory.Delete(_outputDir, recursive: true);
			} catch { }
		}

		// it is up to the user of the V3 abstractor to index created streams, simulate that here.
		// this is because we are currently using the normal event index to look up the stream names
		bool GetOrReserve(string streamName, out StreamId streamId, out StreamId createdId, out string createdName) {
			_sut.StreamNameIndex.GetOrReserve(
				recordFactory: _sut.RecordFactory,
				streamName: streamName,
				logPosition: 123,
				streamId: out streamId,
				streamRecord: out var record);
			
			if (record is LogV3StreamRecord streamRecord) {
				createdId = streamRecord.Record.SubHeader.ReferenceNumber;
				createdName = streamRecord.StreamName;
				_mockIndexReader.Add(streamRecord.ExpectedVersion + 1, streamRecord);
				_sut.StreamNameIndexConfirmer.Confirm(
					replicatedPrepares: new[] { streamRecord },
					catchingUp: false,
					backend: new MockIndexBackend<StreamId>());
				return false;
			} else {
				createdId = default;
				createdName = default;
				return true;
			}
		}

		bool GetOrReserveEventType(string eventType, out StreamId eventTypeId, out EventTypeId createdId, out string createdName) {
			_sut.EventTypeIndex.GetOrReserveEventType(
				recordFactory: _sut.RecordFactory,
				eventType: eventType,
				logPosition: 456,
				eventTypeId: out eventTypeId,
				eventTypeRecord: out var record);

			if (record is LogV3EventTypeRecord eventTypeRecord) {
				createdId = eventTypeRecord.Record.SubHeader.ReferenceNumber;
				createdName = eventTypeRecord.EventTypeName;
				_mockIndexReader.Add(eventTypeRecord.ExpectedVersion + 1, eventTypeRecord);
				_sut.EventTypeIndexConfirmer.Confirm(
					replicatedPrepares: new[] { eventTypeRecord },
					catchingUp: false,
					backend: new MockIndexBackend<EventTypeId>());
				return false;
			} else {
				createdId = default;
				createdName = default;
				return true;
			}
		}

		[Fact]
		public void can_add_another_event_type() {
			Assert.False(GetOrReserveEventType("new-event-type-1", out var newEventTypeId1, out var createdId1, out var createdName1));
			Assert.False(GetOrReserveEventType("new-event-type-2", out var newEventTypeId2, out var createdId2, out var createdName2));
			Assert.Equal(newEventTypeId1 + 1, newEventTypeId2);
			Assert.Equal(newEventTypeId1, createdId1);
			Assert.Equal(newEventTypeId2, createdId2);
			Assert.Equal("new-event-type-1", createdName1);
			Assert.Equal("new-event-type-2", createdName2);
			Assert.Equal(_numEventTypes + 2, _mockIndexReader.EventTypeCount);
		}
		
		[Fact]
		public void can_find_existing_event_type() {
			Assert.True(GetOrReserveEventType(_eventType, out var eventTypeId, out _, out _));
			Assert.Equal(_numEventTypes, _mockIndexReader.EventTypeCount);
			Assert.Equal(_eventTypeId, eventTypeId);
			_sut.EventTypes.LookupName(_eventTypeId);
			Assert.Equal(_eventType, _sut.EventTypes.LookupName(_eventTypeId));
		}
		
		[Theory]
		[InlineData(0, ""/* empty event type */)]
		[InlineData(1, "$event-type")]
		[InlineData(2, "$stream")]
		[InlineData(3, "$metadata")] 
		[InlineData(4, "$streamDeleted")]
		public void can_find_virtual_event_type(EventTypeId expectedId, string name) {
			Assert.True(GetOrReserveEventType(name, out var eventTypeId, out _, out _));
			Assert.Equal(_numEventTypes, _mockIndexReader.EventTypeCount);
			Assert.Equal(expectedId, eventTypeId);
			Assert.Equal(name, _sut.EventTypes.LookupName(expectedId));
		}

		[Fact]
		public void uses_correct_event_type_interval() {
			var expectedEventTypeNumber1 = LogV3SystemEventTypes.FirstRealEventTypeNumber + _numEventTypes;
			Assert.False(GetOrReserveEventType("new-event-type-1", out var newEventTypeId1, out var createdId1, out var createdName1));
			Assert.False(GetOrReserveEventType("new-event-type-2", out var newEventTypeId2, out var createdId2, out var createdName2));
			Assert.Equal(expectedEventTypeNumber1, newEventTypeId1);
			Assert.Equal(expectedEventTypeNumber1 + 1, newEventTypeId2);
			Assert.True(_sut.EventTypes.TryGetName((uint)expectedEventTypeNumber1, out var eventType1));
			Assert.True(_sut.EventTypes.TryGetName((uint)expectedEventTypeNumber1 + 1, out var eventType2));
		}
		
		[Fact]
		public void can_add_another_stream() {
			Assert.False(GetOrReserve("new-stream-1", out var newStreamId1, out var createdId1, out var createdName1));
			Assert.False(GetOrReserve("new-stream-2", out var newStreamId2, out var createdId2, out var createdName2));
			Assert.Equal(newStreamId1 + 2, newStreamId2);
			Assert.Equal(newStreamId1, createdId1);
			Assert.Equal(newStreamId2, createdId2);
			Assert.Equal("new-stream-1", createdName1);
			Assert.Equal("new-stream-2", createdName2);
			Assert.Equal(_numStreams + 2, _mockIndexReader.StreamCount);
		}

		[Fact]
		public void can_add_another_meta_stream() {
			Assert.False(GetOrReserve("$$new-stream-3", out var newStreamId1, out var createdId1, out var createdName1));
			Assert.False(GetOrReserve("$$new-stream-4", out var newStreamId2, out var createdId2, out var createdName2));
			Assert.Equal(_numStreams + 2, _mockIndexReader.StreamCount);

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
			Assert.False(GetOrReserve("$new-stream-5", out var newStreamId1, out var createdId1, out var createdName1));
			Assert.False(GetOrReserve("$new-stream-6", out var newStreamId2, out var createdId2, out var createdName2));
			Assert.Equal(_numStreams + 2, _mockIndexReader.StreamCount);

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
			Assert.False(GetOrReserve("$$$new-stream-7", out var newStreamId1, out var createdId1, out var createdName1));
			Assert.False(GetOrReserve("$$$new-stream-8", out var newStreamId2, out var createdId2, out var createdName2));
			Assert.Equal(_numStreams + 2, _mockIndexReader.StreamCount);

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
			Assert.True(GetOrReserve(_stream, out var streamId, out _, out _));
			Assert.Equal(_numStreams, _mockIndexReader.StreamCount);
			Assert.Equal(_streamId, streamId);
			Assert.Equal(_streamId, _sut.StreamIds.LookupValue(_stream));
			Assert.Equal(_stream, _sut.StreamNames.LookupName(_streamId));
			Assert.False(_sut.SystemStreams.IsMetaStream(streamId));
			Assert.False(_sut.SystemStreams.IsSystemStream(streamId));
			Assert.True(_sut.StreamExistenceFilter.MightContain(_stream));
		}

		[Fact]
		public void can_find_existing_meta_stream() {
			var metaStreamName = "$$" + _stream;
			var expectedMetaStreamId = _streamId + 1;
			Assert.True(GetOrReserve(metaStreamName, out var streamId, out _, out _));
			Assert.Equal(_numStreams, _mockIndexReader.StreamCount);
			Assert.Equal(expectedMetaStreamId, streamId);
			Assert.Equal(expectedMetaStreamId, _sut.StreamIds.LookupValue(metaStreamName));
			Assert.Equal(metaStreamName, _sut.StreamNames.LookupName(expectedMetaStreamId));
			Assert.True(_sut.SystemStreams.IsMetaStream(streamId));
			Assert.True(_sut.SystemStreams.IsSystemStream(streamId));
		}

		[Fact]
		public void can_find_existing_system_stream() {
			Assert.True(GetOrReserve(_systemStream, out var streamId, out _, out _));
			Assert.Equal(_numStreams, _mockIndexReader.StreamCount);
			Assert.Equal(_systemStreamId, streamId);
			Assert.Equal(_systemStreamId, _sut.StreamIds.LookupValue(_systemStream));
			Assert.Equal(_systemStream, _sut.StreamNames.LookupName(_systemStreamId));
			Assert.False(_sut.SystemStreams.IsMetaStream(streamId));
			Assert.True(_sut.SystemStreams.IsSystemStream(streamId));
		}

		[Fact]
		public void can_find_existing_system_meta_stream() {
			var metaStreamName = "$$" + _systemStream;
			var expectedMetaStreamId = _systemStreamId + 1;
			Assert.True(GetOrReserve(metaStreamName, out var streamId, out _, out _));
			Assert.Equal(_numStreams, _mockIndexReader.StreamCount);
			Assert.Equal(expectedMetaStreamId, streamId);
			Assert.Equal(expectedMetaStreamId, _sut.StreamIds.LookupValue(metaStreamName));
			Assert.Equal(metaStreamName, _sut.StreamNames.LookupName(expectedMetaStreamId));
			Assert.True(_sut.SystemStreams.IsMetaStream(streamId));
			Assert.True(_sut.SystemStreams.IsSystemStream(streamId));
		}

		[Theory]
		[InlineData(4, "$all")]
		[InlineData(6, "$streams-created")]
		[InlineData(8, "$settings")]
		public void can_find_virtual_stream(StreamId expectedId, string name) {
			Assert.True(GetOrReserve(name, out var streamId, out _, out _));
			Assert.Equal(_numStreams, _mockIndexReader.StreamCount);
			Assert.Equal(expectedId, streamId);
			Assert.Equal(expectedId, _sut.StreamIds.LookupValue(name));
			Assert.Equal(name, _sut.StreamNames.LookupName(expectedId));
			Assert.False(_sut.SystemStreams.IsMetaStream(streamId));
			Assert.True(_sut.SystemStreams.IsSystemStream(streamId));
		}

		[Fact]
		public void can_find_virtual_meta_stream() {
			Assert.True(GetOrReserve("$$$all", out var streamId, out _, out _));
			Assert.Equal(_numStreams, _mockIndexReader.StreamCount);
			Assert.Equal(5U, streamId);
			Assert.Equal(5U, _sut.StreamIds.LookupValue("$$$all"));
			Assert.Equal("$$$all", _sut.StreamNames.LookupName(5));
			Assert.True(_sut.SystemStreams.IsMetaStream(streamId));
			Assert.True(_sut.SystemStreams.IsSystemStream(streamId));
		}

		[Theory]
		[InlineData(LogV3SystemStreams.NoUserStream, false, false, "new-user-stream")]
		[InlineData(LogV3SystemStreams.NoUserMetastream, true, true, "$$new-user-stream")]
		[InlineData(LogV3SystemStreams.NoSystemStream, false, true, "$new-system-stream")]
		[InlineData(LogV3SystemStreams.NoSystemMetastream, true, true, "$$$new-system-stream")]
		public void can_attempt_to_lookup_non_existent_streams(StreamId expectedId, bool expectedIsMeta, bool expectedIsSystem, string name) {
			Assert.Equal(expectedId, _sut.StreamIds.LookupValue(name));
			Assert.Equal(expectedIsMeta, _sut.SystemStreams.IsMetaStream(expectedId));
			Assert.Equal(expectedIsSystem, _sut.SystemStreams.IsSystemStream(expectedId));
		}

		[Theory]
		[InlineData("")]
		[InlineData("$$new-user-stream")]
		[InlineData("$all")]
		public void cannot_add_certain_kinds_of_streams_to_filter(string name) {
			Assert.Throws<ArgumentException>(() => _sut.StreamExistenceFilter.Add(name));
		}

		class MockIndexReader : IIndexReader<StreamId> {
			private Dictionary<StreamId, Dictionary<long, IPrepareLogRecord<StreamId>>> _index = new() {
				{LogV3SystemStreams.StreamsCreatedStreamNumber, new()},
				{LogV3SystemStreams.EventTypesStreamNumber, new()} 
			};
			
			public void Add(long eventNumber, IPrepareLogRecord<StreamId> record) => _index[record.EventStreamId].Add(eventNumber, record);
			
			public int StreamCount => _index[LogV3SystemStreams.StreamsCreatedStreamNumber].Count;
			public int EventTypeCount => _index[LogV3SystemStreams.EventTypesStreamNumber].Count;

			public IPrepareLogRecord<StreamId> ReadPrepare(StreamId streamId, long eventNumber) {
				// simulates what would be in the index.
				return _index[streamId][eventNumber];
			}

			public long CachedStreamInfo => throw new NotImplementedException();

			public long NotCachedStreamInfo => throw new NotImplementedException();

			public long HashCollisions => throw new NotImplementedException();

			public StorageMessage.EffectiveAcl GetEffectiveAcl(StreamId streamId) =>
				throw new NotImplementedException();

			public IndexReadEventInfoResult ReadEventInfo_KeepDuplicates(uint streamId, long eventNumber) {
				throw new NotImplementedException();
			}

			public StreamId GetEventStreamIdByTransactionId(long transactionId) =>
				throw new NotImplementedException();

			public long GetStreamLastEventNumber(StreamId streamId) {
				if (streamId == LogV3SystemStreams.StreamsCreatedStreamNumber)
					return _index[streamId].Count - 1;
				throw new NotImplementedException();
			}

			public StreamMetadata GetStreamMetadata(StreamId streamId) =>
				throw new NotImplementedException();

			public IndexReadEventResult ReadEvent(string streamName, StreamId streamId, long eventNumber) =>
				throw new NotImplementedException();

			public IndexReadStreamResult ReadStreamEventsBackward(string streamName, StreamId streamId, long fromEventNumber, int maxCount) =>
				throw new NotImplementedException();

			public IndexReadStreamResult ReadStreamEventsForward(string streamName, StreamId streamId, long fromEventNumber, int maxCount) =>
				throw new NotImplementedException();

			public IndexReadEventInfoResult ReadEventInfoForward_KnownCollisions(uint streamId, long fromEventNumber, int maxCount, long beforePosition) =>
				throw new NotImplementedException();

			public IndexReadEventInfoResult ReadEventInfoForward_NoCollisions(ulong stream, long fromEventNumber, int maxCount, long beforePosition) =>
				throw new NotImplementedException();

			public IndexReadEventInfoResult ReadEventInfoBackward_KnownCollisions(uint streamId, long fromEventNumber, int maxCount, long beforePosition) =>
				throw new NotImplementedException();

			public IndexReadEventInfoResult ReadEventInfoBackward_NoCollisions(ulong stream, Func<ulong, uint> getStreamId, long fromEventNumber, int maxCount, long beforePosition) =>
				throw new NotImplementedException();

			public long GetStreamLastEventNumber_KnownCollisions(uint streamId, long beforePosition) =>
				throw new NotImplementedException();

			public long GetStreamLastEventNumber_NoCollisions(ulong stream, Func<ulong, uint> getStreamId, long beforePosition) =>
				throw new NotImplementedException();
		}
	}

	public class MockIndexBackend<TStreamId> : IIndexBackend<TStreamId> {
		public TFReaderLease BorrowReader() {
			throw new NotImplementedException();
		}

		public SystemSettings GetSystemSettings() {
			throw new NotImplementedException();
		}

		public long? SetStreamLastEventNumber(TStreamId streamId, long lastEventNumber) {
			return null;
		}

		public StreamMetadata SetStreamMetadata(TStreamId streamId, StreamMetadata metadata) {
			throw new NotImplementedException();
		}

		public void SetSystemSettings(SystemSettings systemSettings) {
			throw new NotImplementedException();
		}

		public IndexBackend<TStreamId>.EventNumberCached TryGetStreamLastEventNumber(TStreamId streamId) {
			throw new NotImplementedException();
		}

		public IndexBackend<TStreamId>.MetadataCached TryGetStreamMetadata(TStreamId streamId) {
			throw new NotImplementedException();
		}

		public long? UpdateStreamLastEventNumber(int cacheVersion, TStreamId streamId, long? lastEventNumber) {
			throw new NotImplementedException();
		}

		public StreamMetadata UpdateStreamMetadata(int cacheVersion, TStreamId streamId, StreamMetadata metadata) {
			throw new NotImplementedException();
		}
	}
}
