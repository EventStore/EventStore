using EventStore.Common.Utils;
using EventStore.Core.LogAbstraction;

namespace EventStore.Core.LogV2 {
	public class LogV2StreamNameIndex :
		INameIndex<string>,
		INameIndexConfirmer<string>,
		IValueLookup<string>,
		INameLookup<string> {

		public LogV2StreamNameIndex() {
		}

		public void Dispose() {
		}

		public void InitializeWithConfirmed(INameLookup<string> source) {
		}

		public void CancelReservations() {
		}

		public void Confirm(string name, string value) {
		}

		public bool GetOrReserve(string streamName, out string streamId, out string createdId, out string createdName) {
			Ensure.NotNullOrEmpty(streamName, "streamName");
			streamId = streamName;
			createdId = default;
			createdName = default;
			return true;
		}

		public void Reserve(string streamName, out string streamId, out string createdId, out string createdName) {
			Ensure.NotNullOrEmpty(streamName, "streamName");
			streamId = streamName;
			createdId = default;
			createdName = default;
		}

		public string LookupValue(string streamName) => streamName;

		public bool TryGetName(string value, out string name) {
			name = value;
			return true;
		}

		public bool TryGetLastValue(out string last) {
			throw new System.NotImplementedException();
		}
	}
}
