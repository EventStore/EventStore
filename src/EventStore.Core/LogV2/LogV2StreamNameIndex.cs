using EventStore.Core.LogAbstraction;

namespace EventStore.Core.LogV2 {
	public class LogV2StreamNameIndex :
		IStreamNameIndex<string>,
		IStreamIdLookup<string>,
		IStreamNameLookup<string> {

		public LogV2StreamNameIndex() {
		}

		public bool GetOrAddId(string streamName, out string streamId) {
			streamId = streamName;
			return true;
		}

		public string LookupId(string streamName) => streamName;
		public string LookupName(string streamId) => streamId;
	}
}
