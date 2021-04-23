using EventStore.Core.LogAbstraction;
using EventStore.Core.LogV3;
using Xunit;

namespace EventStore.Core.Tests.XUnit.LogAbstraction {
	// check that lookups of the various combinations of virtual/normal/meta
	// work in both directions and in the stream index.
	// todo: ideally check that no unexpected (i.e. meta or virtual) streams have been
	// added to the underlying index after each test.
	public class LogFormatAbstractorV3Tests {
		readonly LogFormatAbstractor<long> _sut = LogFormatAbstractor.V3;
		readonly string _stream = "account-abc";
		readonly string _systemStream = "$something-parked";
		readonly long _streamId;
		readonly long _systemStreamId;

		public LogFormatAbstractorV3Tests() {
			_sut.StreamNamesProvider.SetReader(null);
			_sut.StreamNameIndex.GetOrAddId(_stream, out _streamId, out _, out _);
			_sut.StreamNameIndex.GetOrAddId(_systemStream, out _systemStreamId, out _, out _);
		}

		[Fact]
		public void can_add_another_stream() {
			Assert.False(_sut.StreamNameIndex.GetOrAddId("new-stream-1", out var newStreamId1, out var createdId1, out var createdName1));
			Assert.False(_sut.StreamNameIndex.GetOrAddId("new-stream-2", out var newStreamId2, out var createdId2, out var createdName2));
			Assert.Equal(newStreamId1 + 2, newStreamId2);
			Assert.Equal(newStreamId1, createdId1);
			Assert.Equal(newStreamId2, createdId2);
			Assert.Equal("new-stream-1", createdName1);
			Assert.Equal("new-stream-2", createdName2);
		}

		[Fact]
		public void can_add_another_meta_stream() {
			Assert.False(_sut.StreamNameIndex.GetOrAddId("$$new-stream-3", out var newStreamId1, out var createdId1, out var createdName1));
			Assert.False(_sut.StreamNameIndex.GetOrAddId("$$new-stream-4", out var newStreamId2, out var createdId2, out var createdName2));

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
			Assert.False(_sut.StreamNameIndex.GetOrAddId("$new-stream-5", out var newStreamId1, out var createdId1, out var createdName1));
			Assert.False(_sut.StreamNameIndex.GetOrAddId("$new-stream-6", out var newStreamId2, out var createdId2, out var createdName2));

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
			Assert.False(_sut.StreamNameIndex.GetOrAddId("$$$new-stream-7", out var newStreamId1, out var createdId1, out var createdName1));
			Assert.False(_sut.StreamNameIndex.GetOrAddId("$$$new-stream-8", out var newStreamId2, out var createdId2, out var createdName2));

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
			Assert.True(_sut.StreamNameIndex.GetOrAddId(_stream, out var streamId, out _, out _));
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
			Assert.True(_sut.StreamNameIndex.GetOrAddId(metaStreamName, out var streamId, out _, out _));
			Assert.Equal(expectedMetaStreamId, streamId);
			Assert.Equal(expectedMetaStreamId, _sut.StreamIds.LookupId(metaStreamName));
			Assert.Equal(metaStreamName, _sut.StreamNames.LookupName(expectedMetaStreamId));
			Assert.True(_sut.SystemStreams.IsMetaStream(streamId));
			Assert.True(_sut.SystemStreams.IsSystemStream(streamId));
		}

		[Fact]
		public void can_find_existing_system_stream() {
			Assert.True(_sut.StreamNameIndex.GetOrAddId(_systemStream, out var streamId, out _, out _));
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
			Assert.True(_sut.StreamNameIndex.GetOrAddId(metaStreamName, out var streamId, out _, out _));
			Assert.Equal(expectedMetaStreamId, streamId);
			Assert.Equal(expectedMetaStreamId, _sut.StreamIds.LookupId(metaStreamName));
			Assert.Equal(metaStreamName, _sut.StreamNames.LookupName(expectedMetaStreamId));
			Assert.True(_sut.SystemStreams.IsMetaStream(streamId));
			Assert.True(_sut.SystemStreams.IsSystemStream(streamId));
		}

		[Theory]
		[InlineData(4, "$all")]
		[InlineData(6, "$streams-created")]
		[InlineData(8, "$settings")]
		public void can_find_virtual_stream(long expectedId, string name) {
			Assert.True(_sut.StreamNameIndex.GetOrAddId(name, out var streamId, out _, out _));
			Assert.Equal(expectedId, streamId);
			Assert.Equal(expectedId, _sut.StreamIds.LookupId(name));
			Assert.Equal(name, _sut.StreamNames.LookupName(expectedId));
			Assert.False(_sut.SystemStreams.IsMetaStream(streamId));
			Assert.True(_sut.SystemStreams.IsSystemStream(streamId));
		}

		[Fact]
		public void can_find_virtual_meta_stream() {
			Assert.True(_sut.StreamNameIndex.GetOrAddId("$$$all", out var streamId, out _, out _));
			Assert.Equal(5, streamId);
			Assert.Equal(5, _sut.StreamIds.LookupId("$$$all"));
			Assert.Equal("$$$all", _sut.StreamNames.LookupName(5));
			Assert.True(_sut.SystemStreams.IsMetaStream(streamId));
			Assert.True(_sut.SystemStreams.IsSystemStream(streamId));
		}

		[Theory]
		[InlineData(LogV3SystemStreams.NoUserStream, false, false, "new-user-stream")]
		[InlineData(LogV3SystemStreams.NoUserMetastream, true, true, "$$new-user-stream")]
		[InlineData(LogV3SystemStreams.NoSystemStream, false, true, "$new-system-stream")]
		[InlineData(LogV3SystemStreams.NoSystemMetastream, true, true, "$$$new-system-stream")]
		public void can_foo(long expectedId, bool expectedIsMeta, bool expectedIsSystem, string name) {
			Assert.Equal(expectedId, _sut.StreamIds.LookupId(name));
			Assert.Equal(expectedIsMeta, _sut.SystemStreams.IsMetaStream(expectedId));
			Assert.Equal(expectedIsSystem, _sut.SystemStreams.IsSystemStream(expectedId));
		}
	}
}
