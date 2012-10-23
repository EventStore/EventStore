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

        public override string ToString()
        {
            return string.Format("EventId: {0}, Type: {1}, Data: {2}, Metadata: {3}",
                                 EventId,
                                 Type,
                                 Encoding.UTF8.GetString(Data ?? new byte[0]),
                                 Encoding.UTF8.GetString(Metadata ?? new byte[0]));
        }
    }

    internal static class TestEventsComparer
    {
        public static bool Equal(TestEvent expected, RecordedEvent actual)
        {
            if (expected.EventId != actual.EventId)
                return false;

            if (expected.Type != actual.EventType)
                return false;

            var expectedDataString = Encoding.UTF8.GetString(expected.Data ?? new byte[0]);
            var expectedMetadataString = Encoding.UTF8.GetString(expected.Metadata ?? new byte[0]);

            var actualDataString = Encoding.UTF8.GetString(actual.Data ?? new byte[0]);
            var actualMetadataDataString = Encoding.UTF8.GetString(actual.Metadata ?? new byte[0]);

            return expectedDataString == actualDataString && expectedMetadataString == actualMetadataDataString;
        }

        public static bool Equal(TestEvent[] expected, RecordedEvent[] actual)
        {
            if (expected.Length != actual.Length)
                return false;

            for (int i = 0; i < expected.Length; i++)
            {
                if (!Equal(expected[i], actual[i]))
                    return false;
            }

            return true;
        }
    }
}
