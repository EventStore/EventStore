using EventStore.ClientAPI.Internal;
using EventStore.ClientAPI.Messages;

namespace EventStore.ClientAPI {
	/// <summary>
	/// The result of a read operation from the $all stream.
	/// </summary>
	public class AllEventsSlice {
		/// <summary>
		/// The direction of read request.
		/// </summary>
		public readonly ReadDirection ReadDirection;

		/// <summary>
		/// A <see cref="Position"/> representing the position where this slice was read from.
		/// </summary>
		public readonly Position FromPosition;

		/// <summary>
		/// A <see cref="Position"/> representing the position where the next slice should be read from.
		/// </summary>
		public readonly Position NextPosition;

		/// <summary>
		/// The events read.
		/// </summary>
		public readonly ResolvedEvent[] Events;

		/// <summary>
		/// A boolean representing whether or not this is the end of the $all stream.
		/// </summary>
		public bool IsEndOfStream {
			get { return Events.Length == 0; }
		}

		internal AllEventsSlice(ReadDirection readDirection, Position fromPosition, Position nextPosition,
			ClientMessage.ResolvedEvent[] events) {
			ReadDirection = readDirection;
			FromPosition = fromPosition;
			NextPosition = nextPosition;
			if (events == null)
				Events = Empty.ResolvedEvents;
			else {
				Events = new ResolvedEvent[events.Length];
				for (int i = 0; i < Events.Length; ++i) {
					Events[i] = new ResolvedEvent(events[i]);
				}
			}
		}
	}
}
