using EventStore.Common.Utils;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Services;

namespace EventStore.Core.LogV3 {
	// Decorates a StreamNameIndex, intercepting Metastream (and VirtualStream) calls
	public class StreamNameIndexMetastreamDecorator : IStreamNameIndex<long> {
		private readonly IStreamNameIndex<long> _wrapped;
		private readonly IMetastreamLookup<long> _metastreams;

		public StreamNameIndexMetastreamDecorator(
			IStreamNameIndex<long> wrapped,
			IMetastreamLookup<long> metastreams) {

			_wrapped = wrapped;
			_metastreams = metastreams;
		}

		public bool GetOrAddId(string streamName, out long streamId, out long createdId, out string createdName) {
			Ensure.NotNullOrEmpty(streamName, "streamName");
			if (SystemStreams.IsMetastream(streamName)) {
				streamName = SystemStreams.OriginalStreamOf(streamName);
				var ret = GetOrAddId(streamName, out streamId, out createdId, out createdName);
				streamId = _metastreams.MetaStreamOf(streamId);
				return ret;
			}

			if (LogV3SystemStreams.TryGetVirtualStreamId(streamName, out streamId)) {
				createdId = default;
				createdName = default;
				return true;
			}

			return _wrapped.GetOrAddId(streamName, out streamId, out createdId, out createdName);
		}
	}
}
