using System;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.LogAbstraction {
	public static class INameIndexExtensions {
		// todo: rename to GetOrReserveStream when we generalise to EventTypes too.
		/// Generates a StreamRecord if necessary
		public static void GetOrReserve<TStreamId>(
			this INameIndex<TStreamId> streamNameIndex,
			IRecordFactory<TStreamId> recordFactory,
			string streamName,
			long logPosition,
			out TStreamId streamId,
			out IPrepareLogRecord<TStreamId> streamRecord) {

			var preExisting = streamNameIndex.GetOrReserve(streamName, out streamId, out var addedId, out var addedName);

			var appendNewStream = recordFactory.ExplicitStreamCreation && !preExisting;
			if (!appendNewStream) {
				streamRecord = null;
				return;
			}

			streamRecord = recordFactory.CreateStreamRecord(
				streamId: Guid.NewGuid(),
				logPosition: logPosition,
				timeStamp: DateTime.UtcNow,
				streamNumber: addedId,
				streamName: addedName);
		}
	}
}
