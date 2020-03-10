using System;
using System.Text.RegularExpressions;

namespace EventStore.Client {
#if EVENTSTORE_GRPC_PUBLIC
	public
#else
	internal
#endif
		struct RegularFilterExpression : IEquatable<RegularFilterExpression> {
		public static readonly RegularFilterExpression None = default;

		public static readonly RegularFilterExpression ExcludeSystemEvents =
			new RegularFilterExpression(new Regex(@"^[^\$].*"));

		private readonly string _value;

		public RegularFilterExpression(string value) {
			if (value == null) throw new ArgumentNullException(nameof(value));
			_value = value;
		}

		public RegularFilterExpression(Regex value) {
			if (value == null) throw new ArgumentNullException(nameof(value));
			_value = value.ToString();
		}

		public bool Equals(RegularFilterExpression other) => string.Equals(_value, other._value);

		public override bool Equals(object obj) => obj is RegularFilterExpression other && Equals(other);
		public override int GetHashCode() => _value?.GetHashCode() ?? 0;

		public static bool operator ==(RegularFilterExpression left, RegularFilterExpression right) =>
			left.Equals(right);

		public static bool operator !=(RegularFilterExpression left, RegularFilterExpression right) =>
			!left.Equals(right);

		public static implicit operator string(RegularFilterExpression value) => value.ToString();
		public override string ToString() => _value;
	}
}
