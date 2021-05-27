using System;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Services;

namespace EventStore.Core.LogV3 {
	// Decorates a StreamIdLookup, intercepting Metastream (and VirtualStream) calls
	public class StreamIdLookupMetastreamDecorator : IValueLookup<long> {
		private readonly IValueLookup<long> _wrapped;
		private readonly IMetastreamLookup<long> _metastreams;

		public StreamIdLookupMetastreamDecorator(
			IValueLookup<long> wrapped,
			IMetastreamLookup<long> metastreams) {

			_wrapped = wrapped;
			_metastreams = metastreams;
		}

		public long LookupValue(string streamName) {
			if (string.IsNullOrEmpty(streamName))
				throw new ArgumentNullException(nameof(streamName));

			long streamId;
			if (SystemStreams.IsMetastream(streamName)) {
				streamName = SystemStreams.OriginalStreamOf(streamName);
				streamId = LookupValue(streamName);
				return _metastreams.MetaStreamOf(streamId);
			}

			if (LogV3SystemStreams.TryGetVirtualStreamId(streamName, out streamId))
				return streamId;

			var result = _wrapped.LookupValue(streamName);

			return result == default
				? SystemStreams.IsSystemStream(streamName)
					? LogV3SystemStreams.NoSystemStream
					: LogV3SystemStreams.NoUserStream
				: result;
		}
	}
}
