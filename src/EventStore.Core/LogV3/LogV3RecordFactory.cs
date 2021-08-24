using System;
using EventStore.Core.LogAbstraction;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.LogCommon;
using EventStore.LogV3;
using StreamId = System.UInt32;

namespace EventStore.Core.LogV3 {
	public class LogV3RecordFactory : IRecordFactory<StreamId> {
		private Guid _rootPartitionId;

		public LogV3RecordFactory() {
			if (!BitConverter.IsLittleEndian) {
				// to support big endian we would need to adjust some of the bit
				// operations in the raw v3 structs, and adjust the way that the
				// v3 records are written/read from disk (currently blitted)
				throw new NotSupportedException();
			}
		}

		public bool ExplicitStreamCreation => true;
		public bool MultipleEventsPerPrepare => true;

		public IPrepareLogRecord<StreamId> CreateStreamRecord(
			Guid streamId,
			long logPosition,
			DateTime timeStamp,
			StreamId streamNumber,
			string streamName) {

			var result = new LogV3StreamRecord(
				streamId: streamId,
				logPosition: logPosition,
				timeStamp: timeStamp,
				streamNumber: streamNumber,
				streamName: streamName,
				partitionId: _rootPartitionId);

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

		public IPrepareLogRecord<StreamId> CreatePrepare(
			long logPosition,
			Guid correlationId,
			long transactionPosition,
			int transactionOffset,
			StreamId eventStreamId,
			long expectedVersion,
			DateTime timeStamp,
			PrepareFlags flags,
			IEventRecord[] events) {

			var result = new LogV3StreamWriteRecord(
				logPosition: logPosition,
				correlationId: correlationId,
				eventStreamId: eventStreamId,
				expectedVersion: expectedVersion,
				timeStamp: timeStamp,
				prepareFlags: flags,
				events: events);
			return result;
		}

		public ILogRecord CreatePartitionTypeRecord(
			DateTime timeStamp,
			long logPosition,
			Guid partitionTypeId,
			Guid partitionId,
			string name) {
			
			return new PartitionTypeLogRecord(
				timeStamp: timeStamp,
				logPosition: logPosition,
				partitionTypeId: partitionTypeId,
				partitionId: partitionId,
				name: name
			);
		}

		public ILogRecord CreatePartitionRecord(
			DateTime timeStamp,
			long logPosition,
			Guid partitionId,
			Guid partitionTypeId,
			Guid parentPartitionId,
			Raw.PartitionFlags flags,
			ushort referenceNumber,
			string name) {
			
			return new PartitionLogRecord(
				timeStamp: timeStamp,
				logPosition: logPosition,
				partitionId: partitionId,
				partitionTypeId: partitionTypeId,
				parentPartitionId: parentPartitionId,
				flags: flags,
				referenceNumber: referenceNumber,
				name: name
			);
		}

		public void SetRootPartitionId(Guid id) {
			_rootPartitionId = id;
		}
	}
}
