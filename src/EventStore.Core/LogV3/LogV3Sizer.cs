using EventStore.Core.LogAbstraction;

namespace EventStore.Core.LogV3 {
	public class LogV3Sizer : ISizer<long> {
		public int GetSizeInBytes(long t) => sizeof(long);
	}
}
