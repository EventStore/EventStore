using EventStore.Core.LogAbstraction;
using EventStore.Core.Services;

namespace EventStore.Core.LogV3 {
	// Decorates a StreamNameLookup, intercepting Metastream (and VirtualStream) calls
	public class StreamNameLookupMetastreamDecorator : IStreamNameLookup<long> {
		private readonly IStreamNameLookup<long> _wrapped;
		private readonly IMetastreamLookup<long> _metastreams;

		public StreamNameLookupMetastreamDecorator(
			IStreamNameLookup<long> wrapped,
			IMetastreamLookup<long> metastreams) {

			_wrapped = wrapped;
			_metastreams = metastreams;
		}

		public string LookupName(long streamId) {
			string name;
			if (_metastreams.IsMetaStream(streamId)) {
				streamId = _metastreams.OriginalStreamOf(streamId);
				name = LookupName(streamId);
				name = SystemStreams.MetastreamOf(name);
				return name;
			}

			if (LogV3SystemStreams.TryGetVirtualStreamName(streamId, out name))
				return name;

			name = _wrapped.LookupName(streamId);
			return name;
		}
	}
}
