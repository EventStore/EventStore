using EventStore.Core.LogAbstraction;
using EventStore.Core.Services;

namespace EventStore.Core.LogV3 {
	// Decorates a StreamNameLookup, intercepting Metastream (and VirtualStream) calls
	public class StreamNameLookupMetastreamDecorator : INameLookup<long> {
		private readonly INameLookup<long> _wrapped;
		private readonly IMetastreamLookup<long> _metastreams;

		public StreamNameLookupMetastreamDecorator(
			INameLookup<long> wrapped,
			IMetastreamLookup<long> metastreams) {

			_wrapped = wrapped;
			_metastreams = metastreams;
		}

		public bool TryGetName(long streamId, out string name) {
			if (_metastreams.IsMetaStream(streamId)) {
				streamId = _metastreams.OriginalStreamOf(streamId);
				if (!TryGetName(streamId, out name))
					return false;
				name = SystemStreams.MetastreamOf(name);
				return true;
			}

			if (LogV3SystemStreams.TryGetVirtualStreamName(streamId, out name))
				return true;

			return _wrapped.TryGetName(streamId, out name);
		}

		public bool TryGetLastValue(out long last) {
			return _wrapped.TryGetLastValue(out last);
		}
	}
}
