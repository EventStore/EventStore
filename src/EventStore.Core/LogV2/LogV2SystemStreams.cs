using EventStore.Core.LogAbstraction;
using EventStore.Core.Services;

namespace EventStore.Core.LogV2 {
	public class LogV2SystemStreams : ISystemStreamLookup<string> {
		public LogV2SystemStreams() {
		}

		public string AllStream { get; } = SystemStreams.AllStream;
		public string SettingsStream { get; } = SystemStreams.SettingsStream;

		public bool IsMetaStream(string streamId) => SystemStreams.IsMetastream(streamId);
		public bool IsSystemStream(string streamId) => SystemStreams.IsSystemStream(streamId);
		public string MetaStreamOf(string streamId) => SystemStreams.MetastreamOf(streamId);
		public string OriginalStreamOf(string streamId) => SystemStreams.OriginalStreamOf(streamId);
	}
}
