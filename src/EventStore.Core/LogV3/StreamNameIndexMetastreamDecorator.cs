using EventStore.Common.Utils;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Services;

namespace EventStore.Core.LogV3 {
	// Decorates a StreamNameIndex, intercepting Metastream (and VirtualStream) calls
	public class StreamNameIndexMetastreamDecorator : INameIndex<long> {
		private readonly INameIndex<long> _wrapped;
		private readonly IMetastreamLookup<long> _metastreams;

		public StreamNameIndexMetastreamDecorator(
			INameIndex<long> wrapped,
			IMetastreamLookup<long> metastreams) {

			_wrapped = wrapped;
			_metastreams = metastreams;
		}

		public void CancelReservations() {
			_wrapped.CancelReservations();
		}

		public bool GetOrReserve(string streamName, out long streamId, out long createdId, out string createdName) {
			Ensure.NotNullOrEmpty(streamName, "streamName");
			if (SystemStreams.IsMetastream(streamName)) {
				streamName = SystemStreams.OriginalStreamOf(streamName);
				var ret = GetOrReserve(streamName, out streamId, out createdId, out createdName);
				streamId = _metastreams.MetaStreamOf(streamId);
				return ret;
			}

			if (LogV3SystemStreams.TryGetVirtualStreamId(streamName, out streamId)) {
				createdId = default;
				createdName = default;
				return true;
			}

			return _wrapped.GetOrReserve(streamName, out streamId, out createdId, out createdName);
		}
	}
}
