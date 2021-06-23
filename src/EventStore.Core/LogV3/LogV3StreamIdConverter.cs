using EventStore.Core.LogAbstraction;
using StreamId = System.UInt32;

namespace EventStore.Core.LogV3 {
	public class LogV3StreamIdConverter : IStreamIdConverter<StreamId> {
		public StreamId ToStreamId(StreamId x) => x;
	}
}
