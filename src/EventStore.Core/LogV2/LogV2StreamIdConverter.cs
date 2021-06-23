using EventStore.Core.LogAbstraction;
using LogV3StreamId = System.UInt32;

namespace EventStore.Core.LogV2 {
	public class LogV2StreamIdConverter : IStreamIdConverter<string> {
		public string ToStreamId(LogV3StreamId x) {
			throw new System.NotImplementedException();
		}
	}
}
