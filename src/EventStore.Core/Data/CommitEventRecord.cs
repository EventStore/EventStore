namespace EventStore.Core.Data {
	public struct CommitEventRecord {
		public readonly EventRecord Event;
		public readonly long CommitPosition;

		public CommitEventRecord(EventRecord @event, long commitPosition) {
			Event = @event;
			CommitPosition = commitPosition;
		}

		public override string ToString() {
			return string.Format("CommitPosition: {0}, Event: {1}", CommitPosition, Event);
		}
	}
}
