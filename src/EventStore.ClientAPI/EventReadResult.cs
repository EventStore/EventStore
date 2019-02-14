using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Messages;

namespace EventStore.ClientAPI {
	/// <summary>
	/// A event read result is the result of a single event read operation to Event Store.
	/// </summary>
	public class EventReadResult {
		/// <summary>
		/// The <see cref="EventReadStatus"/> representing the status of this read attempt.
		/// </summary>
		public readonly EventReadStatus Status;

		/// <summary>
		/// The name of the stream read.
		/// </summary>
		public readonly string Stream;

		/// <summary>
		/// The event number of the requested event.
		/// </summary>
		public readonly long EventNumber;

		/// <summary>
		/// The event read represented as <see cref="ResolvedEvent"/>.
		/// </summary>
		public readonly ResolvedEvent? Event;

		internal EventReadResult(EventReadStatus status,
			string stream,
			long eventNumber,
			ClientMessage.ResolvedIndexedEvent @event) {
			Ensure.NotNullOrEmpty(stream, "stream");

			Status = status;
			Stream = stream;
			EventNumber = eventNumber;
			Event = status == EventReadStatus.Success ? new ResolvedEvent(@event) : (ResolvedEvent?)null;
		}
	}
}
