using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace EventStore.Grpc {
#if EVENTSTORE_GRPC_PUBLIC
	public
#else
	internal
#endif
		interface IEventFilter {
		PrefixFilterExpression[] Prefixes { get; }
		RegularFilterExpression Regex { get; }
		int? MaxSearchWindow { get; }
	}
#if EVENTSTORE_GRPC_PUBLIC
	public
#else
	internal
#endif
		struct StreamFilter : IEquatable<StreamFilter>, IEventFilter {
		public PrefixFilterExpression[] Prefixes { get; }
		public RegularFilterExpression Regex { get; }
		public int? MaxSearchWindow { get; }

		public static readonly StreamFilter None = default;

		public StreamFilter(RegularFilterExpression regex) : this(default, regex) { }

		public StreamFilter(int? maxSearchWindow, RegularFilterExpression regex) {
			Regex = regex;
			Prefixes = Array.Empty<PrefixFilterExpression>();
			MaxSearchWindow = maxSearchWindow;
		}

		public StreamFilter(params PrefixFilterExpression[] prefixes) : this(default, prefixes) { }

		public StreamFilter(int? maxSearchWindow, params PrefixFilterExpression[] prefixes) {
			if (prefixes.Length == 0) {
				throw new ArgumentException();
			}

			Prefixes = prefixes;
			Regex = RegularFilterExpression.None;
			MaxSearchWindow = maxSearchWindow;
		}

		public bool Equals(StreamFilter other) =>
			Prefixes.SequenceEqual(other.Prefixes) &&
			Regex.Equals(other.Regex) &&
			MaxSearchWindow.Equals(other.MaxSearchWindow);

		public override bool Equals(object obj) => obj is StreamFilter other && Equals(other);
		public override int GetHashCode() => HashCode.Hash.Combine(Prefixes).Combine(Regex).Combine(MaxSearchWindow);
		public static bool operator ==(StreamFilter left, StreamFilter right) => left.Equals(right);
		public static bool operator !=(StreamFilter left, StreamFilter right) => !left.Equals(right);
	}

#if EVENTSTORE_GRPC_PUBLIC
	public
#else
	internal
#endif
		struct EventTypeFilter : IEquatable<EventTypeFilter>, IEventFilter {
		public PrefixFilterExpression[] Prefixes { get; }
		public RegularFilterExpression Regex { get; }
		public int? MaxSearchWindow { get; }

		public static readonly EventTypeFilter None = default;

		public EventTypeFilter(RegularFilterExpression regex) : this(default, regex) { }

		public EventTypeFilter(int? maxSearchWindow, RegularFilterExpression regex) {
			Regex = regex;
			Prefixes = Array.Empty<PrefixFilterExpression>();
			MaxSearchWindow = maxSearchWindow;
		}

		public EventTypeFilter(params PrefixFilterExpression[] prefixes) : this(default, prefixes) { }

		public EventTypeFilter(int? maxSearchWindow, params PrefixFilterExpression[] prefixes) {
			if (prefixes.Length == 0) {
				throw new ArgumentException();
			}

			Prefixes = prefixes;
			Regex = RegularFilterExpression.None;
			MaxSearchWindow = maxSearchWindow;
		}

		public bool Equals(EventTypeFilter other) =>
			Prefixes.SequenceEqual(other.Prefixes) &&
			Regex.Equals(other.Regex) &&
			MaxSearchWindow.Equals(other.MaxSearchWindow);

		public override bool Equals(object obj) => obj is EventTypeFilter other && Equals(other);
		public override int GetHashCode() => HashCode.Hash.Combine(Prefixes).Combine(Regex).Combine(MaxSearchWindow);
		public static bool operator ==(EventTypeFilter left, EventTypeFilter right) => left.Equals(right);
		public static bool operator !=(EventTypeFilter left, EventTypeFilter right) => !left.Equals(right);
	}

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
