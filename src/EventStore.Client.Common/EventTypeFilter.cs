using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace EventStore.Client {
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

		public static EventTypeFilter ExcludeSystemEvents(uint maxSearchWindow = 32) =>
			new EventTypeFilter(maxSearchWindow, RegularFilterExpression.ExcludeSystemEvents);


		public static IEventFilter Prefix(string prefix)
			=> new EventTypeFilter(new PrefixFilterExpression(prefix));

		public static IEventFilter Prefix(params string[] prefixes)
			=> new EventTypeFilter(Array.ConvertAll(prefixes, prefix => new PrefixFilterExpression(prefix)));

		public static IEventFilter Prefix(uint maxSearchWindow, params string[] prefixes)
			=> new EventTypeFilter(maxSearchWindow,
				Array.ConvertAll(prefixes, prefix => new PrefixFilterExpression(prefix)));

		public static IEventFilter RegularExpression(string regex, uint maxSearchWindow = 32)
			=> new EventTypeFilter(maxSearchWindow, new RegularFilterExpression(regex));

		public static IEventFilter RegularExpression(Regex regex, uint maxSearchWindow = 32)
			=> new EventTypeFilter(maxSearchWindow, new RegularFilterExpression(regex));

		private EventTypeFilter(RegularFilterExpression regex) : this(default, regex) { }

		private EventTypeFilter(uint maxSearchWindow, RegularFilterExpression regex) {
			if (maxSearchWindow == 0) {
				throw new ArgumentOutOfRangeException(nameof(maxSearchWindow),
					maxSearchWindow, $"{nameof(maxSearchWindow)} must be greater than 0.");
			}

			Regex = regex;
			Prefixes = Array.Empty<PrefixFilterExpression>();
			MaxSearchWindow = maxSearchWindow;
		}

		private EventTypeFilter(params PrefixFilterExpression[] prefixes) : this(32, prefixes) { }

		public EventTypeFilter(uint maxSearchWindow, params PrefixFilterExpression[] prefixes) {
			if (prefixes.Length == 0) {
				throw new ArgumentException();
			}

			if (maxSearchWindow == 0) {
				throw new ArgumentOutOfRangeException(nameof(maxSearchWindow),
					maxSearchWindow, $"{nameof(maxSearchWindow)} must be greater than 0.");
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
	}}
