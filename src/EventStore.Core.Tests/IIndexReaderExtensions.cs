using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog;

namespace EventStore.Core.Tests {
	public static class IIndexReaderExtensions {
		public static IndexReadEventResult ReadEvent(this IIndexReader<string> index, string streamName, long eventNumber) =>
			index.ReadEvent(streamName, streamName, eventNumber, ITransactionFileTracker.NoOp);

		public static IndexReadStreamResult ReadStreamEventsBackward(this IIndexReader<string> index, string streamName, long fromEventNumber, int maxCount) =>
			index.ReadStreamEventsBackward(streamName, streamName, fromEventNumber, maxCount, ITransactionFileTracker.NoOp);

		public static IndexReadStreamResult ReadStreamEventsForward(this IIndexReader<string> index, string streamName, long fromEventNumber, int maxCount) =>
			index.ReadStreamEventsForward(streamName, streamName, fromEventNumber, maxCount, ITransactionFileTracker.NoOp);
	}
}
