using System;

namespace EventStore.Client {
	internal static class DeadLine {
		public static DateTime? After(TimeSpan? timeoutAfter) =>
			timeoutAfter.HasValue ? DateTime.UtcNow.Add(timeoutAfter.Value) : (DateTime?)null;

		public static TimeSpan? None = null;
	}
}
