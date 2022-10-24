using System.Diagnostics;

namespace EventStore.Core.Diagnostics {
	//qq this is more strongly typed than just passing a long representing the number of ticks
	// and provides us a place to change the resolution and size if long ticks is overkill
	public struct Instant {
		private static readonly float _ticksPerSecond = Stopwatch.Frequency;
		//qq consider if this will cause anything daft to happen wrt rounding etc.
		private static readonly float _secondsPerTick = 1 / _ticksPerSecond;
		private static readonly float _millisecondsPerTick = _secondsPerTick * 1000;
		private static readonly float _microsecondsPerTick = _millisecondsPerTick * 1000;

		//qq necessary tick based? we might want to use something more coarse gained and smaller for performance.
		private readonly long _ticks;

		public Instant(long ticks) {
			_ticks = ticks;
		}

		public static float TicksToSeconds(long ticks) => ticks * _secondsPerTick;
		public static float TicksToMicroseconds(long ticks) => ticks * _microsecondsPerTick;

		public long ElapsedTicks(Instant since) => _ticks - since._ticks;

		//qq float resolution might do?
		public float ElapsedSeconds(Instant since) => TicksToSeconds(ElapsedTicks(since));
		public float ElapsedMicroseconds(Instant since) => TicksToMicroseconds(ElapsedTicks(since));
	}
}
