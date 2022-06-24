using EventStore.Core.Helpers;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog.Scavenging {
	public struct RecordForAccumulatorInitParams<TStreamId> : IReusableObjectInitParams {
		public readonly PrepareLogRecordView PrepareView;
		public readonly TStreamId StreamId;
		public RecordForAccumulatorInitParams(PrepareLogRecordView prepareLogRecordView, TStreamId streamId) {
			PrepareView = prepareLogRecordView;
			StreamId = streamId;
		}
	}
}
