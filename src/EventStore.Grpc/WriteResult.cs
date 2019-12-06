using System;

namespace EventStore.Grpc {
	public struct WriteResult : IEquatable<WriteResult> {
		public readonly long NextExpectedVersion;
		public readonly Position LogPosition;

		public WriteResult(long nextExpectedVersion, Position logPosition) {
			NextExpectedVersion = nextExpectedVersion;
			LogPosition = logPosition;
		}

		public bool Equals(WriteResult other) =>
			NextExpectedVersion == other.NextExpectedVersion && LogPosition.Equals(other.LogPosition);

		public override bool Equals(object obj) => obj is WriteResult other && Equals(other);
		public static bool operator ==(WriteResult left, WriteResult right) => left.Equals(right);
		public static bool operator !=(WriteResult left, WriteResult right) => !left.Equals(right);
		public override int GetHashCode() => HashCode.Hash.Combine(NextExpectedVersion).Combine(LogPosition);

		public override string ToString() => $"{NextExpectedVersion}:{LogPosition}";
	}
}
