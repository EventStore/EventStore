using System;

namespace EventStore.ClientAPI
{
    public interface IEvent
    {
        Guid EventId { get; }
        string Type { get; }

        byte[] Data { get; }
        byte[] Metadata { get; }
    }
}