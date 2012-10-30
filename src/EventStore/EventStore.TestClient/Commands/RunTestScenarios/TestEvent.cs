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
            var subIndex = (index % 50);

            EventId = Guid.NewGuid();
            Type = subIndex.ToString();

            var body = new string('#', 1 + 17 * subIndex * subIndex);

            Data = Encoding.UTF8.GetBytes(string.Format("{0}-{1}-{2}", index, body.Length, body));
            Metadata = new byte[0];
        }
    }
}