namespace EventStore.Core.Data
{
    public struct EventLinkPair
    {
        public readonly EventRecord Event;
        public readonly EventRecord Link;

        public EventLinkPair(EventRecord @event, EventRecord link)
        {
            Event = @event;
            Link = link;
        }

        public EventLinkPair(EventRecord @event)
        {
            Event = @event;
            Link = null;
        }
    }
}