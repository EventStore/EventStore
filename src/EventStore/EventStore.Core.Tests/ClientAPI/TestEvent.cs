using System;
using System.Text;
using EventStore.ClientAPI;

namespace EventStore.Core.Tests.ClientAPI
{
    internal class TestEvent : IEvent
    {
        public Guid EventId { get; private set; }
        public string Type { get; private set; }

        public byte[] Data { get; private set; }
        public byte[] Metadata { get; private set; }

        public TestEvent(string data = null, string metadata = null)
        {
            EventId = Guid.NewGuid();
            Type = GetType().FullName;

            Data = Encoding.UTF8.GetBytes(data ?? EventId.ToString());
            Metadata = Encoding.UTF8.GetBytes(metadata ?? "metadata");
        }
    }
}
