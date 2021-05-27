using System;
using EventStore.Core.LogAbstraction;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.LogV3 {
	public class LogV3RecordFactory : IRecordFactory<long> {
		public LogV3RecordFactory() {
			if (!BitConverter.IsLittleEndian) {
				// to support big endian we would need to adjust some of the bit
				// operatiosn in the raw v3 structs, and adjust the way that the
				// v3 records are written/read from disk (currently blitted)
				throw new NotSupportedException();
			}
		}

		public bool ExplicitStreamCreation => true;

		public IPrepareLogRecord<long> CreateStreamRecord(
			Guid streamId,
			long logPosition,
			DateTime timeStamp,
			long streamNumber,
			string streamName) {

			var result = new LogV3StreamRecord(
				streamId: streamId,
				logPosition: logPosition,
				timeStamp: timeStamp,
				streamNumber: (uint)streamNumber, // todo: switch to uint
				streamName: streamName);

			return result;
		}

		public ISystemLogRecord CreateEpoch(EpochRecord epoch) {
			var result = new LogV3EpochLogRecord(
				logPosition: epoch.EpochPosition,
				timeStamp: epoch.TimeStamp,
				epochNumber: epoch.EpochNumber,
				epochId: epoch.EpochId,
				prevEpochPosition: epoch.PrevEpochPosition,
				leaderInstanceId: epoch.LeaderInstanceId);
			return result;
		}

		public IPrepareLogRecord<long> CreatePrepare(
			long logPosition,
			Guid correlationId,
			Guid eventId,
			long transactionPosition,
			int transactionOffset,
			long eventStreamId,
			long expectedVersion,
			DateTime timeStamp,
			PrepareFlags flags,
			string eventType,
			ReadOnlyMemory<byte> data,
			ReadOnlyMemory<byte> metadata) {

			var result = new LogV3StreamWriteRecord(
				logPosition: logPosition,
				transactionPosition: transactionPosition,
				transactionOffset: transactionOffset,
				correlationId: correlationId,
				eventId: eventId,
				eventStreamId: eventStreamId,
				expectedVersion: expectedVersion,
				timeStamp: timeStamp,
				flags: flags,
				eventType: eventType,
				data: data.Span,
				metadata: metadata.Span);
			return result;
		}
	}
}
