using System.Diagnostics;

namespace EventStore.Core.Diagnostics {
	//qq a static source of time. see what james did in his and probably just do that
	static class Time {
		private static readonly Stopwatch _stopwatch = Stopwatch.StartNew();

		//qq due to the high volume, we might wnat to use a smaller datastructure than this. but we also probably want to be able to measure
		// sub millisecond
		//qqq name to `Now`?
		public static Instant CurrentInstant => new(_stopwatch.ElapsedTicks);
	}
}
