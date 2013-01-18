using System.Text;
using EventStore.ClientAPI;

namespace EventStore.Core.Tests.ClientAPI.Helpers
{
    internal static class EventDataComparer
    {
        public static bool Equal(EventData expected, RecordedEvent actual)
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

        public static bool Equal(EventData[] expected, RecordedEvent[] actual)
        {
            if (expected.Length != actual.Length)
                return false;

            for (var i = 0; i < expected.Length; i++)
            {
                if (!Equal(expected[i], actual[i]))
                    return false;
            }

            return true;
        }
    }
}