using EventStore.LogCommon;

namespace EventStore.LogV3 {
	static class EventFlagsExtensions {
		public static Raw.EventFlags Convert(this EventFlags eventFlags) {
			Raw.EventFlags flags = Raw.EventFlags.None;
			if((eventFlags & EventFlags.IsJson) != 0) {
				flags |= Raw.EventFlags.IsJson;
			}
			return flags;
		}

		public static EventFlags Convert(this Raw.EventFlags eventFlags) {
			EventFlags flags = EventFlags.None;
			if((eventFlags & Raw.EventFlags.IsJson) != 0) {
				flags |= EventFlags.IsJson;
			}
			return flags;
		}
	}
}
