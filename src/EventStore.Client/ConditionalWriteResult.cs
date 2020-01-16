using System;

namespace EventStore.Client {
	public struct ConditionalWriteResult : IEquatable<ConditionalWriteResult> {
		public static readonly ConditionalWriteResult StreamDeleted =
			new ConditionalWriteResult(-1, Position.End, ConditionalWriteStatus.StreamDeleted);

		public readonly long NextExpectedVersion;
		public readonly Position LogPosition;
		public readonly ConditionalWriteStatus Status;

		private ConditionalWriteResult(long nextExpectedVersion, Position logPosition,
			ConditionalWriteStatus status = ConditionalWriteStatus.Succeeded) {
			NextExpectedVersion = nextExpectedVersion;
			LogPosition = logPosition;
			Status = status;
		}

		public static ConditionalWriteResult FromWriteResult(WriteResult writeResult)
			=> new ConditionalWriteResult(writeResult.NextExpectedVersion, writeResult.LogPosition);

		public static ConditionalWriteResult FromWrongExpectedVersion(WrongExpectedVersionException ex)
			=> new ConditionalWriteResult(ex.ExpectedVersion ?? -1, Position.End,
				ConditionalWriteStatus.VersionMismatch);

		public bool Equals(ConditionalWriteResult other) => NextExpectedVersion == other.NextExpectedVersion &&
		                                                    LogPosition.Equals(other.LogPosition) &&
		                                                    Status == other.Status;

		public override bool Equals(object obj) => obj is ConditionalWriteResult other && Equals(other);

		public override int GetHashCode() =>
			HashCode.Hash.Combine(NextExpectedVersion).Combine(LogPosition).Combine(Status);

		public static bool operator ==(ConditionalWriteResult left, ConditionalWriteResult right) => left.Equals(right);

		public static bool operator !=(ConditionalWriteResult left, ConditionalWriteResult right) =>
			!left.Equals(right);

		public override string ToString() => $"{Status}:{NextExpectedVersion}:{LogPosition}";
	}
}
