using System;
using System.Text;
using EventStore.ClientAPI;

namespace EventStore.TestClient.Commands.RunTestScenarios
{
    internal class TestEvent : IEvent
    {
        public Guid EventId { get; private set; }
        public string Type { get; private set; }

        public byte[] Data { get; private set; }
        public byte[] Metadata { get; private set; }

        public TestEvent(int index)
        {
            EventId = Guid.NewGuid();
            Type = index.ToString();

            Data = Encoding.UTF8.GetBytes(string.Format("{0}-{1}", index, new string('#', 1024)));
            Metadata = new byte[0];
        }
    }
}