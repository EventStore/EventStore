using System;
using System.Diagnostics;

namespace EventStore.Core.Time;

// this provides stronger typing than just passing a long representing the number of ticks
// and provides us a place to change the resolution and size if long ticks is overkill.
public struct Instant : IEquatable<Instant> {
	public static readonly long TicksPerSecond;
	private static readonly double SecondsPerTick;
	private static readonly long TicksPerTimeSpanTick;

	static Instant() {
		TicksPerSecond = Stopwatch.Frequency;
		SecondsPerTick =  1 / (double)TicksPerSecond;

		if (TicksPerSecond % TimeSpan.TicksPerSecond != 0)
			throw new Exception($"Expected {nameof(TicksPerSecond)} ({TicksPerSecond}) to be an exact multiple" +
			                    $" of {nameof(TimeSpan)}.{nameof(TimeSpan.TicksPerSecond)} ({TimeSpan.TicksPerSecond})");

		TicksPerTimeSpanTick = TicksPerSecond / TimeSpan.TicksPerSecond;
	}

	public static Instant Now => new(Stopwatch.GetTimestamp());
	public static Instant FromSeconds(long seconds) => new(stopwatchTicks: seconds * TicksPerSecond);

	public static bool operator ==(Instant x, Instant y) => x._ticks == y._ticks;
	public static bool operator !=(Instant x, Instant y) => x._ticks != y._ticks;
	public static bool operator <=(Instant x, Instant y) => x._ticks <= y._ticks;
	public static bool operator >=(Instant x, Instant y) => x._ticks >= y._ticks;
	public static bool operator <(Instant x, Instant y) => x._ticks < y._ticks;
	public static bool operator >(Instant x, Instant y) => x._ticks > y._ticks;

	private static double TicksToSeconds(long ticks) => ticks * SecondsPerTick;

	private readonly long _ticks;

	// Stopwatch Ticks, not DateTime Ticks - these can be different.
	private Instant(long stopwatchTicks) {
		_ticks = stopwatchTicks;
	}

	public long Ticks => _ticks;

	public double ElapsedSecondsSince(Instant start) => TicksToSeconds(ElapsedTicksSince(start));

	public Instant Add(TimeSpan timeSpan) {
		return new(_ticks + (long)(timeSpan.TotalSeconds * TicksPerSecond));
	}

	// something has gone wrong if we call this
	public override bool Equals(object obj) =>
		throw new InvalidOperationException();

	public bool Equals(Instant that) =>
		this == that;

	public override int GetHashCode() =>
		_ticks.GetHashCode();

	/// Stopwatch Ticks, not DateTime Ticks.
	public long ElapsedTicksSince(Instant since) => _ticks - since._ticks;

	public TimeSpan ElapsedTimeSince(Instant since) {
		var elapsedTicks = ElapsedTicksSince(since);
		// since we're decreasing the resolution when converting to TimeSpan, we round up to make sure that something
		// using the TimeSpan doesn't wait for less time than it should.
		elapsedTicks = (elapsedTicks + TicksPerTimeSpanTick - 1) / TicksPerTimeSpanTick;
		return new TimeSpan(elapsedTicks);
	}
}
