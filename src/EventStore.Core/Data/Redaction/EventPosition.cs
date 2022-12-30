namespace EventStore.Core.Data.Redaction {
	public readonly struct EventPosition {
		public readonly long LogPosition;

		public EventPosition(long logPosition) {
			LogPosition = logPosition;
		}
	}
}
