using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Messages;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// A Event Read Result is the result of a single event read operation to the event store.
    /// </summary>
    public class EventReadResult
    {
        /// <summary>
        /// The <see cref="EventReadStatus"/> representing the status of this read attempt
        /// </summary>
        public readonly EventReadStatus Status;

        /// <summary>
        /// The name of the stream read
        /// </summary>
        public readonly string Stream;

        /// <summary>
        /// The event number of the requested event.
        /// </summary>
        public readonly int EventNumber;

        /// <summary>
        /// The event read represented as <see cref="ResolvedEvent"/>
        /// </summary>
        public readonly ResolvedEvent? Event;

        internal EventReadResult(EventReadStatus status, 
                                 string stream, 
                                 int eventNumber, 
                                 ClientMessage.ResolvedIndexedEvent @event)
        {
            Ensure.NotNullOrEmpty(stream, "stream");

            Status = status;
            Stream = stream;
            EventNumber = eventNumber;
            Event = status == EventReadStatus.Success ? new ResolvedEvent(@event) : (ResolvedEvent?)null;
        }
    }
}