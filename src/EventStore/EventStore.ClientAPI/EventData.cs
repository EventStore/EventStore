using System;

namespace EventStore.ClientAPI
{
    public sealed class EventData
    {
        public readonly Guid EventId;
        public readonly string Type;
        public readonly bool IsJson;
        public readonly byte[] Data;
        public readonly byte[] Metadata;

        public EventData(Guid eventId, string type, bool isJson, byte[] data, byte[] metadata)
        {
            EventId = eventId;
            Type = type;
            IsJson = isJson;
            Data = data ?? Empty.ByteArray;
            Metadata = metadata ?? Empty.ByteArray;
        }
    }
}