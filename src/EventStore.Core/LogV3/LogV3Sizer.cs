using EventStore.Core.LogAbstraction;
using StreamId = System.UInt32;

namespace EventStore.Core.LogV3 {
	public class LogV3Sizer : ISizer<StreamId> {
		public int GetSizeInBytes(StreamId t) => sizeof(StreamId);
	}
}
