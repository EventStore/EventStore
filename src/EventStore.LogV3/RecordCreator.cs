using System;
using EventStore.LogCommon;

namespace EventStore.LogV3 {
	// Nice-to-use wrappers for creating and populating the raw structures.
	public static class RecordCreator {
		public static RecordView<Raw.EpochHeader> CreateEpochRecord(
			DateTime timeStamp,
			long logPosition,
			int epochNumber,
			Guid epochId,
			long prevEpochPosition,
			Guid leaderInstanceId) {

			var record = MutableRecordView<Raw.EpochHeader>.Create(payloadLength: 0);
			ref var header = ref record.Header;
			ref var epoch = ref record.SubHeader;

			header.Type = LogRecordType.System;
			header.Version = LogRecordVersion.LogRecordV1;
			header.TimeStamp = timeStamp;
			header.RecordId = epochId;
			header.LogPosition = logPosition;

			epoch.EpochNumber = epochNumber;
			epoch.LeaderInstanceId = leaderInstanceId;
			epoch.PrevEpochPosition = prevEpochPosition;

			return record;
		}
	}
}
