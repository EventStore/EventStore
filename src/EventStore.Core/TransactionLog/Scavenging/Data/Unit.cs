using System;

namespace EventStore.Core.TransactionLog.Scavenging {
	public struct Unit : IEquatable<Unit> {
		public static readonly Unit Instance = new Unit();

		public override int GetHashCode() => 1;
		public override bool Equals(object other) => other is Unit;
		public bool Equals(Unit other) => true;
	}
}
