using System;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Data;

namespace EventStore.Core.TransactionLog.LogRecords {
	public static class EpochRecordExtensions {
		public static string AsString(this EpochRecord epoch) {
			return string.Format("E{0}@{1}:{2:B}",
				epoch == null ? -1 : epoch.EpochNumber,
				epoch == null ? -1 : epoch.EpochPosition,
				epoch == null ? Guid.Empty : epoch.EpochId);
		}

		public static string AsString(this Epoch epoch) {
			return string.Format("E{0}@{1}:{2:B}",
				epoch == null ? -1 : epoch.EpochNumber,
				epoch == null ? -1 : epoch.EpochPosition,
				epoch == null ? Guid.Empty : epoch.EpochId);
		}
	}

	public class EpochRecord {
		public readonly long EpochPosition;
		public readonly int EpochNumber;
		public readonly Guid EpochId;

		public readonly long PrevEpochPosition;
		public readonly DateTime TimeStamp;
		public readonly Guid LeaderInstanceId;

		public EpochRecord(long epochPosition, int epochNumber, Guid epochId, long prevEpochPosition,
			DateTime timeStamp, Guid leaderInstanceId) {
			EpochPosition = epochPosition;
			EpochNumber = epochNumber;
			EpochId = epochId;
			PrevEpochPosition = prevEpochPosition;
			TimeStamp = timeStamp;
			LeaderInstanceId = leaderInstanceId;
		}

		internal EpochRecord(EpochRecordDto dto)
			: this(dto.EpochPosition, dto.EpochNumber, dto.EpochId, dto.PrevEpochPosition, dto.TimeStamp, dto.LeaderInstanceId) {
		}

		public byte[] AsSerialized() {
			return new EpochRecordDto(this).ToJsonBytes();
		}

		public override string ToString() {
			return string.Format(
				"EpochPosition: {0}, EpochNumber: {1}, EpochId: {2}, PrevEpochPosition: {3}, TimeStamp: {4}, LeaderInstanceId: {5}",
				EpochPosition,
				EpochNumber,
				EpochId,
				PrevEpochPosition,
				TimeStamp,
				LeaderInstanceId);
		}

		internal class EpochRecordDto {
			public long EpochPosition { get; set; }
			public int EpochNumber { get; set; }
			public Guid EpochId { get; set; }

			public long PrevEpochPosition { get; set; }
			public DateTime TimeStamp { get; set; }
			public Guid LeaderInstanceId { get; set; }

			public EpochRecordDto() {
			}

			public EpochRecordDto(EpochRecord rec) {
				EpochPosition = rec.EpochPosition;
				EpochNumber = rec.EpochNumber;
				EpochId = rec.EpochId;

				PrevEpochPosition = rec.PrevEpochPosition;
				TimeStamp = rec.TimeStamp;
				LeaderInstanceId = rec.LeaderInstanceId;
			}
		}
	}
}
