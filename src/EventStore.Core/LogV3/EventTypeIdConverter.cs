namespace EventStore.Core.LogV3 {
	/// Converts between EventTypeIds and their event number in the event types stream.
	public static class EventTypeIdConverter {
		static readonly uint _offset = LogV3SystemEventTypes.FirstRealEventTypeNumber;

		public static uint ToEventTypeId(long index) {
			return (uint)index + _offset;
		}

		public static long ToEventNumber(uint eventTypeId) {
			return eventTypeId - _offset;
		}
	}
}
