using System;

namespace EventStore.Client {
	internal static class DeadLine {
		public static DateTime? After(TimeSpan? timeoutAfter) {
			if (!timeoutAfter.HasValue || timeoutAfter == TimeSpan.Zero) {
				return null;
			}
			return DateTime.UtcNow.Add(timeoutAfter.Value);
		}
		
		public static TimeSpan None = TimeSpan.Zero;
	}
}
