using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;

namespace EventStore.Core.Telemetry;

public interface IDurationMaxTracker {
	// Returns the current instant
	Instant RecordNow(Instant start);
}

// When observed, this returns the max duration that it has been asked to record over the last x
// seconds, where x is not exact, but within a known range.
// The idea is, if we expect to take an observation every e.g. ~15 seconds, and that observation
// contains the max duration over the last 16-20 seconds, then every duration will be captured
// at least once (although, some of them will be captured twice, which isn't as bad as missing them).
//
// It is useful for observing values with high volatility that may spike up significantly and then
// return to normal between scrapes and therefore the spike would otherwise be missing from the metrics.
//
// It works by recording max values in buckets, each bucket representing one sub-period.
// The max value for the period is then the max across all the buckets.
// This keeps resource consumption low even when there are a lot of durations recorded.
// On recording/observing, buckets that contain data that is old enough to not be relevant are reset.
//
// Multiple threads can RecordNow and Observe concurrently.
public class DurationMaxTracker : IDurationMaxTracker {
	private readonly long _ticksPerBucket;
	private readonly int _numBuckets;
	private readonly IClock _clock;
	private readonly KeyValuePair<string, object>[] _maxTags;
	private readonly double[] _maxBuckets;
	private readonly object _lock = new();

	// the sub period when this was last accessed (for recording or observation)
	// we use this to determine what data (buckets) have become stale in the mean time
	private long _lastSubPeriod;

	public DurationMaxTracker(
		DurationMaxMetric metric,
		string name,
		int expectedScrapeIntervalSeconds,
		IClock clock = null,
		IBucketCalculator bucketCalculator = null) {

		_clock = clock ?? Clock.Instance;
		bucketCalculator ??= new BucketCalculator();

		bucketCalculator.Calculate(
			expectedScrapeIntervalSeconds: expectedScrapeIntervalSeconds,
			numBuckets: out _numBuckets,
			secondsPerBucket: out var secondsPerBucket,
			minPeriodSeconds: out var minPeriodSeconds,
			maxPeriodSeconds: out var maxPeriodSeconds);

		_ticksPerBucket = secondsPerBucket * Instant.TicksPerSecond;

		_maxTags = new KeyValuePair<string, object>[] {
			new("name", name),
			new("range", $"{minPeriodSeconds}-{maxPeriodSeconds} seconds"),
		};

		_maxBuckets = new double[_numBuckets];

		metric.Add(this);
	}

	public Instant RecordNow(Instant start) {
		var now = _clock.Now;
		var currentSubPeriod = now.Ticks / _ticksPerBucket;

		ResetStaleBuckets(currentSubPeriod);

		var elapsedSeconds = now.ElapsedSecondsSince(start);
		var currentIndex = (int)(currentSubPeriod % _numBuckets);
		_maxBuckets[currentIndex] = Math.Max(_maxBuckets[currentIndex], elapsedSeconds);
		return now;
	}

	public Measurement<double> Observe() {
		ResetStaleBuckets(_clock.Now.Ticks / _ticksPerBucket);
		return new Measurement<double>(_maxBuckets.Max(), _maxTags.AsSpan());
	}

	private void ResetStaleBuckets(long currentSubPeriod) {
		lock (_lock) {
			// reset from the _lastSubPeriod (exclusive) to the currentSubPeriod (inclusive)
			// which means we need to reset n buckets starting from the bucket after the
			// _lastSubPeriod where n is currentSubPeriod - _lastSubPeriod, but at most _numBuckets.
			var numBucketsToReset = Math.Min(currentSubPeriod - _lastSubPeriod, _numBuckets);
			var targetBucket = (_lastSubPeriod + 1) % _numBuckets;
			for (var n = 0; n < numBucketsToReset; n++) {
				_maxBuckets[targetBucket] = 0;
				targetBucket = (targetBucket + 1) % _numBuckets;
			}

			_lastSubPeriod = currentSubPeriod;
		}
	}

	public interface IBucketCalculator {
		void Calculate(
			int expectedScrapeIntervalSeconds,
			out int numBuckets,
			out int secondsPerBucket,
			out int minPeriodSeconds,
			out int maxPeriodSeconds);
	}

	// calculates the number of buckets and the length of time each bucket represents. we calculate
	// these such that the amount of time that observations cover is more than the expected scrape
	// interval, so that we are likely to capture all the spikes even if a scape is a little late.
	// if a scrape is _really_ late, e.g. because a blocking GC pauses all the threads for a long
	// time, then some datapoints to be missed because they have become stale - but they will be
	// the datapoints immediately before the GC, not the spike from the GC itself.
	// we can't improve on this by comparing the time of the scrape with the time of the previous
	// scrape because there might be multiple scrapers.
	public class BucketCalculator : IBucketCalculator {
		public void Calculate(
			int expectedScrapeIntervalSeconds,
			out int numBuckets,
			out int secondsPerBucket,
			out int minPeriodSeconds,
			out int maxPeriodSeconds) {

			if (expectedScrapeIntervalSeconds == 0) {
				// observed durations in the range 0-1s
				numBuckets = 1;
				secondsPerBucket = 1;
			} else if (expectedScrapeIntervalSeconds == 1) {
				// observed durations in the range 2-3s
				numBuckets = 3;
				secondsPerBucket = 1;
			} else if (expectedScrapeIntervalSeconds == 5) {
				// observed durations in the range 6-8s
				numBuckets = 4;
				secondsPerBucket = 2;
			} else if (expectedScrapeIntervalSeconds == 10) {
				// observed durations in the range 12-15s
				numBuckets = 5;
				secondsPerBucket = 3;
			} else if (expectedScrapeIntervalSeconds % 15 == 0) {
				numBuckets = 5;
				secondsPerBucket = expectedScrapeIntervalSeconds / 15 * 4;
			} else {
				throw new ArgumentException(
					$"ExpectedScrapeIntervalSeconds must be 0, 1, 5, 10 or a multiple of 15, " +
					$"but was {expectedScrapeIntervalSeconds}");
			}

			maxPeriodSeconds = numBuckets * secondsPerBucket;
			minPeriodSeconds = maxPeriodSeconds - secondsPerBucket;
		}
	}

	public class NoOp : IDurationMaxTracker {
		public Instant RecordNow(Instant start) => Instant.Now;
	}
}
