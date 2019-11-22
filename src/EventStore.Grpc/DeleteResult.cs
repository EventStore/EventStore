using System;

namespace EventStore.Grpc {
	public struct DeleteResult : IEquatable<DeleteResult> {
		public bool Equals(DeleteResult other) => LogPosition.Equals(other.LogPosition);
		public override bool Equals(object obj) => obj is DeleteResult other && Equals(other);
		public override int GetHashCode() => LogPosition.GetHashCode();
		public static bool operator ==(DeleteResult left, DeleteResult right) => left.Equals(right);
		public static bool operator !=(DeleteResult left, DeleteResult right) => !left.Equals(right);

		public readonly Position LogPosition;

		public DeleteResult(Position logPosition) {
			LogPosition = logPosition;
		}
	}
}
