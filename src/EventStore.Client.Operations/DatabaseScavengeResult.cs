using System;

namespace EventStore.Client.Operations {
	public struct DatabaseScavengeResult : IEquatable<DatabaseScavengeResult> {
		public string ScavengeId { get; }
		public ScavengeResult Result { get; }

		public static DatabaseScavengeResult Started(string scavengeId) =>
			new DatabaseScavengeResult(scavengeId, ScavengeResult.Started);

		public static DatabaseScavengeResult Stopped(string scavengeId) =>
			new DatabaseScavengeResult(scavengeId, ScavengeResult.Stopped);

		public static DatabaseScavengeResult InProgress(string scavengeId) =>
			new DatabaseScavengeResult(scavengeId, ScavengeResult.InProgress);

		private DatabaseScavengeResult(string scavengeId, ScavengeResult result) {
			if (scavengeId == null) throw new ArgumentNullException(nameof(scavengeId));
			ScavengeId = scavengeId;
			Result = result;
		}

		public bool Equals(DatabaseScavengeResult other) => ScavengeId == other.ScavengeId && Result == other.Result;
		public override bool Equals(object obj) => obj is DatabaseScavengeResult other && Equals(other);
		public override int GetHashCode() => HashCode.Hash.Combine(ScavengeId).Combine(Result);
		public static bool operator ==(DatabaseScavengeResult left, DatabaseScavengeResult right) => left.Equals(right);

		public static bool operator !=(DatabaseScavengeResult left, DatabaseScavengeResult right) =>
			!left.Equals(right);
	}
}
