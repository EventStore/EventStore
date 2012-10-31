using EventStore.ClientAPI.Common.Utils;
using ProtoBuf;

namespace EventStore.ClientAPI.Messages
{
    [ProtoContract]
    internal class EventLinkPair
    {
        [ProtoMember(1)]
        public EventRecord Event { get; set; }

        [ProtoMember(2)]
        public EventRecord Link { get; set; }

        public EventLinkPair()
        {
        }

        public EventLinkPair(EventRecord @event, EventRecord link)
        {
            Ensure.NotNull(@event, "event");
            Event = @event;
            Link = link;
        }
    }
}