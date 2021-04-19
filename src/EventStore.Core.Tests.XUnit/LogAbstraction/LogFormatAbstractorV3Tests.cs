using EventStore.Core.LogAbstraction;
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
			_sut.StreamNameIndex.GetOrAddId(_stream, out _streamId);
			_sut.StreamNameIndex.GetOrAddId(_systemStream, out _systemStreamId);
		}

		[Fact] void can_add_another_stream() {
			Assert.False(_sut.StreamNameIndex.GetOrAddId("new-stream-1", out var newStreamId1));
			Assert.False(_sut.StreamNameIndex.GetOrAddId("new-stream-2", out var newStreamId2));
			Assert.Equal(newStreamId1 + 2, newStreamId2);
		}

		[Fact]
		public void can_find_existing_stream() {
			Assert.True(_sut.StreamNameIndex.GetOrAddId(_stream, out var streamId));
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
			Assert.True(_sut.StreamNameIndex.GetOrAddId(metaStreamName, out var streamId));
			Assert.Equal(expectedMetaStreamId, streamId);
			Assert.Equal(expectedMetaStreamId, _sut.StreamIds.LookupId(metaStreamName));
			Assert.Equal(metaStreamName, _sut.StreamNames.LookupName(expectedMetaStreamId));
			Assert.True(_sut.SystemStreams.IsMetaStream(streamId));
			Assert.True(_sut.SystemStreams.IsSystemStream(streamId));
		}

		[Fact]
		public void can_find_existing_system_stream() {
			Assert.True(_sut.StreamNameIndex.GetOrAddId(_systemStream, out var streamId));
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
			Assert.True(_sut.StreamNameIndex.GetOrAddId(metaStreamName, out var streamId));
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
			Assert.True(_sut.StreamNameIndex.GetOrAddId(name, out var streamId));
			Assert.Equal(expectedId, streamId);
			Assert.Equal(expectedId, _sut.StreamIds.LookupId(name));
			Assert.Equal(name, _sut.StreamNames.LookupName(expectedId));
			Assert.False(_sut.SystemStreams.IsMetaStream(streamId));
			Assert.True(_sut.SystemStreams.IsSystemStream(streamId));
		}

		[Fact]
		public void can_find_virtual_meta_stream() {
			Assert.True(_sut.StreamNameIndex.GetOrAddId("$$$all", out var streamId));
			Assert.Equal(3, streamId);
			Assert.Equal(3, _sut.StreamIds.LookupId("$$$all"));
			Assert.Equal("$$$all", _sut.StreamNames.LookupName(3));
			Assert.True(_sut.SystemStreams.IsMetaStream(streamId));
			Assert.True(_sut.SystemStreams.IsSystemStream(streamId));
		}
	}
}
