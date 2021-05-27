using EventStore.Core.LogAbstraction;

namespace EventStore.Core.LogV2 {
	public class LogV2StreamIdConverter : IStreamIdConverter<string> {
		public string ToStreamId(long x) {
			throw new System.NotImplementedException();
		}
	}
}
