namespace EventStore.Core.Data {
	public readonly struct EventInfo {
		public readonly long LogPosition;
		public readonly long EventNumber;

		public EventInfo(long logPosition, long eventNumber) {
			LogPosition = logPosition;
			EventNumber = eventNumber;
		}
	}
}
