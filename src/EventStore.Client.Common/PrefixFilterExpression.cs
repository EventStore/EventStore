using System;

namespace EventStore.Client {
#if EVENTSTORE_GRPC_PUBLIC
	public
#else
	internal
#endif
		struct PrefixFilterExpression : IEquatable<PrefixFilterExpression> {
		public static readonly PrefixFilterExpression None = default;

		private readonly string _value;

		public PrefixFilterExpression(string value) {
			if (value == null) throw new ArgumentNullException(nameof(value));
			_value = value;
		}

		public bool Equals(PrefixFilterExpression other) => string.Equals(_value, other._value);
		public override bool Equals(object obj) => obj is PrefixFilterExpression other && Equals(other);
		public override int GetHashCode() => _value?.GetHashCode() ?? 0;
		public static bool operator ==(PrefixFilterExpression left, PrefixFilterExpression right) => left.Equals(right);

		public static bool operator !=(PrefixFilterExpression left, PrefixFilterExpression right) =>
			!left.Equals(right);

		public static implicit operator string(PrefixFilterExpression value) => value.ToString();
		public override string ToString() => _value;
	}
}
