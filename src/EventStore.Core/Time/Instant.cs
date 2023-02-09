using System;
using System.Diagnostics;

namespace EventStore.Core.Time;

// this provides stronger typing than just passing a long representing the number of ticks
// and provides us a place to change the resolution and size if long ticks is overkill.
public struct Instant : IEquatable<Instant> {
	public static long TicksPerSecond { get; } = Stopwatch.Frequency;

	private static readonly double _secondsPerTick = 1 / (double)TicksPerSecond;

	public static Instant Now => new(Stopwatch.GetTimestamp());
	public static Instant FromSeconds(long seconds) => new(stopwatchTicks: seconds * TicksPerSecond);

	public static bool operator ==(Instant x, Instant y) => x._ticks == y._ticks;
	public static bool operator !=(Instant x, Instant y) => x._ticks != y._ticks;
	public static bool operator <=(Instant x, Instant y) => x._ticks <= y._ticks;
	public static bool operator >=(Instant x, Instant y) => x._ticks >= y._ticks;
	public static bool operator <(Instant x, Instant y) => x._ticks < y._ticks;
	public static bool operator >(Instant x, Instant y) => x._ticks > y._ticks;

	private static double TicksToSeconds(long ticks) => ticks * _secondsPerTick;

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

	private long ElapsedTicksSince(Instant since) => _ticks - since._ticks;
}
