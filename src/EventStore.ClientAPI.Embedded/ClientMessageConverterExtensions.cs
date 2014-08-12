using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Tcp;
using ClientMessage = EventStore.ClientAPI.Messages.ClientMessage;

namespace EventStore.ClientAPI.Embedded
{
    internal static class ClientMessageConverterExtensions
    {
        public static ClientMessage.ResolvedIndexedEvent[] ConvertToResolvedIndexEvents(this EventStore.Core.Data.ResolvedEvent[] events)
        {
            var resolvedEvents = new ClientMessage.ResolvedIndexedEvent[events.Length];

            for (int i = 0; i < events.Length; i++)
            {
                resolvedEvents[i] = events[i].ConvertToResolvedIndexEvent();
            }

            return resolvedEvents;
        }

        public static ClientMessage.ResolvedIndexedEvent ConvertToResolvedIndexEvent(this EventStore.Core.Data.ResolvedEvent @event)
        {
            var dto = new TcpClientMessageDto.ResolvedIndexedEvent(@event.Event, @event.Link);

            return dto.Serialize().Deserialize<ClientMessage.ResolvedIndexedEvent>();
        }

        public static ClientMessage.ResolvedEvent[] ConvertToResolvedEvents(this EventStore.Core.Data.ResolvedEvent[] events)
        {
            var resolvedEvents = new ClientMessage.ResolvedEvent[events.Length];

            for (int i = 0; i < events.Length; i++)
            {
                resolvedEvents[i] = events[i].ConvertToResolvedEvent();
            }

            return resolvedEvents;
        }

        public static ClientMessage.ResolvedEvent ConvertToResolvedEvent(this EventStore.Core.Data.ResolvedEvent @event)
        {
            var dto = new TcpClientMessageDto.ResolvedEvent(@event);
            return dto.Serialize().Deserialize<ClientMessage.ResolvedEvent>();
        }

        public static Event[] ConvertToEvents(this IEnumerable<EventData> events)
        {
            return events.Select(ConvertToEvent).ToArray();
        }

        public static Event ConvertToEvent(this EventData e)
        {
            return new Event(e.EventId, e.Type, e.IsJson, e.Data, e.Metadata);
        }
    }
}