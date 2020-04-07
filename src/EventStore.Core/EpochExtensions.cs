using System;

namespace EventStore.Core {
	internal static class EpochExtensions {
		public static DateTime FromTicksSinceEpoch(this long value) =>
			new DateTime(DateTime.UnixEpoch.Ticks + value, DateTimeKind.Utc);

		public static long ToTicksSinceEpoch(this DateTime value) =>
			(value - DateTime.UnixEpoch).Ticks;
	}
}
