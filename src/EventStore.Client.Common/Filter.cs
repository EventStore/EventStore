using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace EventStore.Client {
#if EVENTSTORE_GRPC_PUBLIC
	public
#else
	internal
#endif
		interface IEventFilter {
		PrefixFilterExpression[] Prefixes { get; }
		RegularFilterExpression Regex { get; }
		uint? MaxSearchWindow { get; }
	}
#if EVENTSTORE_GRPC_PUBLIC
	public
#else
	internal
#endif
		struct StreamFilter : IEquatable<StreamFilter>, IEventFilter {
		public PrefixFilterExpression[] Prefixes { get; }
		public RegularFilterExpression Regex { get; }
		public uint? MaxSearchWindow { get; }

		public static readonly StreamFilter None = default;

		public static IEventFilter Prefix(string prefix)
			=> new StreamFilter(new PrefixFilterExpression(prefix));

		public static IEventFilter Prefix(params string[] prefixes)
			=> new StreamFilter(Array.ConvertAll(prefixes, prefix => new PrefixFilterExpression(prefix)));

		public static IEventFilter Prefix(uint? maxSearchWindow, params string[] prefixes)
			=> new StreamFilter(maxSearchWindow,
				Array.ConvertAll(prefixes, prefix => new PrefixFilterExpression(prefix)));

		public static IEventFilter RegularExpression(string regex)
			=> new StreamFilter(new RegularFilterExpression(regex));

		public static IEventFilter RegularExpression(Regex regex)
			=> new StreamFilter(new RegularFilterExpression(regex));

		public static IEventFilter RegularExpression(string regex, uint? maxSearchWindow)
			=> new StreamFilter(maxSearchWindow, new RegularFilterExpression(regex));

		public static IEventFilter RegularExpression(Regex regex, uint? maxSearchWindow)
			=> new StreamFilter(maxSearchWindow, new RegularFilterExpression(regex));


		private StreamFilter(RegularFilterExpression regex) : this(default, regex) { }

		private StreamFilter(uint? maxSearchWindow, RegularFilterExpression regex) {
			Regex = regex;
			Prefixes = Array.Empty<PrefixFilterExpression>();
			MaxSearchWindow = maxSearchWindow;
		}

		private StreamFilter(params PrefixFilterExpression[] prefixes) : this(default, prefixes) { }

		private StreamFilter(uint? maxSearchWindow, params PrefixFilterExpression[] prefixes) {
			if (prefixes.Length == 0) {
				throw new ArgumentException();
			}

			Prefixes = prefixes;
			Regex = RegularFilterExpression.None;
			MaxSearchWindow = maxSearchWindow;
		}

		public bool Equals(StreamFilter other) =>
			Prefixes == null || other.Prefixes == null
				? Prefixes == other.Prefixes &&
				  Regex.Equals(other.Regex) &&
				  MaxSearchWindow.Equals(other.MaxSearchWindow)
				: Prefixes.SequenceEqual(other.Prefixes) &&
				  Regex.Equals(other.Regex) &&
				  MaxSearchWindow.Equals(other.MaxSearchWindow);

		public override bool Equals(object obj) => obj is StreamFilter other && Equals(other);
		public override int GetHashCode() => HashCode.Hash.Combine(Prefixes).Combine(Regex).Combine(MaxSearchWindow);
		public static bool operator ==(StreamFilter left, StreamFilter right) => left.Equals(right);
		public static bool operator !=(StreamFilter left, StreamFilter right) => !left.Equals(right);

		public override string ToString() =>
			this == None
				? "(none)"
				: $"{nameof(StreamFilter)} {(Prefixes.Length == 0 ? Regex.ToString() : $"[{string.Join(", ", Prefixes)}]")}";
	}

#if EVENTSTORE_GRPC_PUBLIC
	public
#else
	internal
#endif
		struct EventTypeFilter : IEquatable<EventTypeFilter>, IEventFilter {
		public PrefixFilterExpression[] Prefixes { get; }
		public RegularFilterExpression Regex { get; }
		public uint? MaxSearchWindow { get; }

		public static readonly EventTypeFilter None = default;

		public static readonly EventTypeFilter ExcludeSystemEvents =
			new EventTypeFilter(RegularFilterExpression.ExcludeSystemEvents);

		public static IEventFilter Prefix(string prefix)
			=> new EventTypeFilter(new PrefixFilterExpression(prefix));

		public static IEventFilter Prefix(params string[] prefixes)
			=> new EventTypeFilter(Array.ConvertAll(prefixes, prefix => new PrefixFilterExpression(prefix)));

		public static IEventFilter Prefix(uint? maxSearchWindow, params string[] prefixes)
			=> new EventTypeFilter(maxSearchWindow,
				Array.ConvertAll(prefixes, prefix => new PrefixFilterExpression(prefix)));

		public static IEventFilter RegularExpression(string regex)
			=> new EventTypeFilter(new RegularFilterExpression(regex));

		public static IEventFilter RegularExpression(Regex regex)
			=> new EventTypeFilter(new RegularFilterExpression(regex));

		public static IEventFilter RegularExpression(string regex, uint? maxSearchWindow)
			=> new EventTypeFilter(maxSearchWindow, new RegularFilterExpression(regex));

		public static IEventFilter RegularExpression(Regex regex, uint? maxSearchWindow)
			=> new EventTypeFilter(maxSearchWindow, new RegularFilterExpression(regex));

		private EventTypeFilter(RegularFilterExpression regex) : this(default, regex) { }

		private EventTypeFilter(uint? maxSearchWindow, RegularFilterExpression regex) {
			Regex = regex;
			Prefixes = Array.Empty<PrefixFilterExpression>();
			MaxSearchWindow = maxSearchWindow;
		}

		private EventTypeFilter(params PrefixFilterExpression[] prefixes) : this(null, prefixes) { }

		public EventTypeFilter(uint? maxSearchWindow, params PrefixFilterExpression[] prefixes) {
			if (prefixes.Length == 0) {
				throw new ArgumentException();
			}

			Prefixes = prefixes;
			Regex = RegularFilterExpression.None;
			MaxSearchWindow = maxSearchWindow;
		}

		public bool Equals(EventTypeFilter other) =>
			Prefixes == null || other.Prefixes == null
				? Prefixes == other.Prefixes &&
				  Regex.Equals(other.Regex) &&
				  MaxSearchWindow.Equals(other.MaxSearchWindow)
				: Prefixes.SequenceEqual(other.Prefixes) &&
				  Regex.Equals(other.Regex) &&
				  MaxSearchWindow.Equals(other.MaxSearchWindow);

		public override bool Equals(object obj) => obj is EventTypeFilter other && Equals(other);
		public override int GetHashCode() => HashCode.Hash.Combine(Prefixes).Combine(Regex).Combine(MaxSearchWindow);
		public static bool operator ==(EventTypeFilter left, EventTypeFilter right) => left.Equals(right);
		public static bool operator !=(EventTypeFilter left, EventTypeFilter right) => !left.Equals(right);

		public override string ToString() =>
			this == None
				? "(none)"
				: $"{nameof(EventTypeFilter)} {(Prefixes.Length == 0 ? Regex.ToString() : $"[{string.Join(", ", Prefixes)}]")}";
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
