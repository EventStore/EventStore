using EventStore.ClientAPI;

namespace EventStore.Core.Tests.ClientAPI.Helpers {
	internal class EventsStream {
		private const int SliceSize = 10;

		public static int Count(IEventStoreConnection store, string stream) {
			var result = 0;
			while (true) {
				var slice = store.ReadStreamEventsForwardAsync(stream, result, SliceSize, false).Result;
				result += slice.Events.Length;
				if (slice.IsEndOfStream)
					break;
			}

			return result;
		}
	}
}
