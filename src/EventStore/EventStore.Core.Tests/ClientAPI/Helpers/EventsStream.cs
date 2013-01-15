using EventStore.ClientAPI;

namespace EventStore.Core.Tests.ClientAPI.Helpers
{
    internal class EventsStream
    {
        private const int SliceSize = 10;

        public static int Count(EventStoreConnection store, string stream)
        {
            var result = 0;
            while (true)
            {
                var slice = store.ReadStreamEventsForward(stream, result, SliceSize, false);
                result += slice.Events.Length;
                if (slice.IsEndOfStream)
                    break;
            }
            return result;
        }
    }
}
