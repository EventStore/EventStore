using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Internal;
using EventStore.ClientAPI.Messages;

namespace EventStore.ClientAPI {
	/// <summary>
	/// A stream events slice represents the result of a single read operation to Event Store.
	/// </summary>
	public class StreamEventsSlice {
		/// <summary>
		/// The <see cref="SliceReadStatus"/> representing the status of this read attempt.
		/// </summary>
		public readonly SliceReadStatus Status;

		/// <summary>
		/// The name of the stream to read.
		/// </summary>
		public readonly string Stream;

		/// <summary>
		/// The starting point (represented as a sequence number) of the read operation.
		/// </summary>
		public readonly long FromEventNumber;

		/// <summary>
		/// The direction of read request.
		/// </summary>
		public readonly ReadDirection ReadDirection;

		/// <summary>
		/// The events read represented as <see cref="ResolvedEvent"/>.
		/// </summary>
		public readonly ResolvedEvent[] Events;

		/// <summary>
		/// The next event number that can be read.
		/// </summary>
		public readonly long NextEventNumber;

		/// <summary>
		/// The last event number in the stream.
		/// </summary>
		public readonly long LastEventNumber;

		/// <summary>
		/// A boolean representing whether or not this is the end of the stream.
		/// </summary>
		public readonly bool IsEndOfStream;

		internal StreamEventsSlice(SliceReadStatus status,
			string stream,
			long fromEventNumber,
			ReadDirection readDirection,
			ClientMessage.ResolvedIndexedEvent[] events,
			long nextEventNumber,
			long lastEventNumber,
			bool isEndOfStream) {
			Ensure.NotNullOrEmpty(stream, "stream");

			Status = status;
			Stream = stream;
			FromEventNumber = fromEventNumber;
			ReadDirection = readDirection;
			if (events == null || events.Length == 0)
				Events = Empty.ResolvedEvents;
			else {
				Events = new ResolvedEvent[events.Length];
				for (int i = 0; i < Events.Length; ++i) {
					Events[i] = new ResolvedEvent(events[i]);
				}
			}

			NextEventNumber = nextEventNumber;
			LastEventNumber = lastEventNumber;
			IsEndOfStream = isEndOfStream;
		}
	}
}
