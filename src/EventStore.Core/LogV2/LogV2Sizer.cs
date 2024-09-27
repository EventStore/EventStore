using EventStore.Core.Caching;
using EventStore.Core.LogAbstraction;

namespace EventStore.Core.LogV2 {
	public class LogV2Sizer : ISizer<string> {
		public int GetSizeInBytes(string t) => MemSizer.SizeOf(t);
	}
}
