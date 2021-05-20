using EventStore.Core.LogAbstraction;
using EventStore.Core.Services;

namespace EventStore.Core.LogV3 {
	// Decorates a StreamIdLookup, intercepting Metastream (and VirtualStream) calls
	public class StreamIdLookupMetastreamDecorator : IStreamIdLookup<long> {
		private readonly IStreamIdLookup<long> _wrapped;
		private readonly IMetastreamLookup<long> _metastreams;

		public StreamIdLookupMetastreamDecorator(
			IStreamIdLookup<long> wrapped,
			IMetastreamLookup<long> metastreams) {

			_wrapped = wrapped;
			_metastreams = metastreams;
		}

		public long LookupId(string streamName) {
			long streamId;
			if (SystemStreams.IsMetastream(streamName)) {
				streamName = SystemStreams.OriginalStreamOf(streamName);
				streamId = LookupId(streamName);
				return _metastreams.MetaStreamOf(streamId);
			}

			if (LogV3SystemStreams.TryGetVirtualStreamId(streamName, out streamId))
				return streamId;

			var result = _wrapped.LookupId(streamName);

			return result == default
				? SystemStreams.IsSystemStream(streamName)
					? LogV3SystemStreams.NoSystemStream
					: LogV3SystemStreams.NoUserStream
				: result;
		}
	}
}
