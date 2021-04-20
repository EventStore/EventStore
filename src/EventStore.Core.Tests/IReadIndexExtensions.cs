using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.Tests {
	public static class IReadIndexExtensions {
		public static IndexReadEventResult ReadEvent(this IReadIndex<string> index, string streamName, long eventNumber) =>
			index.ReadEvent(streamName, streamName, eventNumber);

		public static IndexReadStreamResult ReadStreamEventsBackward(this IReadIndex<string> index, string streamName, long fromEventNumber, int maxCount) =>
			index.ReadStreamEventsBackward(streamName, streamName, fromEventNumber, maxCount);

		public static IndexReadStreamResult ReadStreamEventsForward(this IReadIndex<string> index, string streamName, long fromEventNumber, int maxCount) =>
			index.ReadStreamEventsForward(streamName, streamName, fromEventNumber, maxCount);
	}
}
