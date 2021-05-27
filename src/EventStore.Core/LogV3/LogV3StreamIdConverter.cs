using EventStore.Core.LogAbstraction;

namespace EventStore.Core.LogV3 {
	public class LogV3StreamIdConverter : IStreamIdConverter<long> {
		public long ToStreamId(long x) => x;
	}
}
