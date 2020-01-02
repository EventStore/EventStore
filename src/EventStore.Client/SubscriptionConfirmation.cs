using System;

namespace EventStore.Client {
	internal struct SubscriptionConfirmation : IEquatable<SubscriptionConfirmation> {
		public static readonly SubscriptionConfirmation None = default;
		public string SubscriptionId { get; }

		public SubscriptionConfirmation(string subscriptionId) {
			if (subscriptionId == null) {
				throw new ArgumentNullException(nameof(subscriptionId));
			}

			SubscriptionId = subscriptionId;
		}

		public bool Equals(SubscriptionConfirmation other) => SubscriptionId == other.SubscriptionId;
		public override bool Equals(object obj) => obj is SubscriptionConfirmation other && Equals(other);
		public override int GetHashCode() => SubscriptionId?.GetHashCode() ?? 0;

		public static bool operator ==(SubscriptionConfirmation left, SubscriptionConfirmation right) =>
			left.Equals(right);

		public static bool operator !=(SubscriptionConfirmation left, SubscriptionConfirmation right) =>
			!left.Equals(right);

		public override string ToString() => SubscriptionId;
	}
}
