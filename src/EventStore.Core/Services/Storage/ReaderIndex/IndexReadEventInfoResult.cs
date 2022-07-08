using EventStore.Core.Data;

namespace EventStore.Core.Services.Storage.ReaderIndex {
	public struct IndexReadEventInfoResult {
		public EventInfo[] EventInfos { get; }
		public long NextEventNumber { get; }
		public bool IsEndOfStream => NextEventNumber < 0;

		public IndexReadEventInfoResult(EventInfo[] eventInfos, long nextEventNumber) {
			EventInfos = eventInfos;
			NextEventNumber = nextEventNumber;
		}
	}
}
