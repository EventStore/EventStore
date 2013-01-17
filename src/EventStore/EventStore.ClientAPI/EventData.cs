using System;

namespace EventStore.ClientAPI
{
    public class EventData
    {
        public Guid EventId { get; protected set; }
        public string Type { get; protected set; }
        public bool IsJson { get; protected set; }
        public byte[] Data { get; protected set; }
        public byte[] Metadata { get; protected set; }

        public EventData(Guid eventId, string type, bool isJson, byte[] data, byte[] metadata)
        {
            EventId = eventId;
            Type = type;
            IsJson = isJson;
            Data = data;
            Metadata = metadata;
        }
    }
}