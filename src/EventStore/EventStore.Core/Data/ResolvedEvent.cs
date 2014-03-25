namespace EventStore.Core.Data
{
    public struct ResolvedEvent
    {
        public static readonly ResolvedEvent[] EmptyArray = new ResolvedEvent[0];

        public readonly EventRecord Event;
        public readonly EventRecord Link;
        public EventRecord OriginalEvent { get { return Link ?? Event; } }

        /// <summary>
        /// Position of the OriginalEvent (unresolved link or event) if available
        /// </summary>
        public readonly TFPos? OriginalPosition;

        public readonly ReadEventResult ResolveResult;

        public string OriginalStreamId { get { return OriginalEvent.EventStreamId; } }
        public int OriginalEventNumber { get { return OriginalEvent.EventNumber; } }

        public ResolvedEvent(
            EventRecord @event, EventRecord link, ReadEventResult resolveResult = default(ReadEventResult))
        {
            Event = @event;
            Link = link;
            OriginalPosition = null;
            ResolveResult = resolveResult;
        }

        public ResolvedEvent(
            EventRecord @event, EventRecord link, long commitPosition,
            ReadEventResult resolveResult = default(ReadEventResult))
        {
            Event = @event;
            Link = link;
            OriginalPosition = new TFPos(commitPosition, (link ?? @event).LogPosition);
            ResolveResult = resolveResult;
        }

        public ResolvedEvent(EventRecord @event)
        {
            Event = @event;
            Link = null;
            OriginalPosition = null;
            ResolveResult = default(ReadEventResult);
        }

        public ResolvedEvent(EventRecord @event, long commitPosition)
        {
            Event = @event;
            Link = null;
            OriginalPosition = new TFPos(commitPosition, @event.LogPosition);
            ResolveResult = default(ReadEventResult);
        }
    }
}