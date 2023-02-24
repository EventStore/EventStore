using System;
using System.Diagnostics.Metrics;
using System.Linq;
using EventStore.Core.Telemetry;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Telemetry;

public class DurationMaxTrackerTests : IDisposable {
	private readonly TestMeterListener<double> _listener;
	private readonly FakeClock _clock = new();
	private readonly DurationMaxTracker _sut;

	public DurationMaxTrackerTests() {
		var meter = new Meter($"{typeof(DurationMaxTrackerTests)}");
		_listener = new TestMeterListener<double>(meter);
		var metric = new DurationMaxMetric(meter, "the-metric");
		_sut = new DurationMaxTracker(
			metric: metric,
			name: "the-tracker",
			expectedScrapeIntervalSeconds: 15,
			clock: _clock);
	}

	public void Dispose() {
		_listener.Dispose();
	}

	[Fact]
	public void throws_with_invalid_period_configuration() {
		var ex = Assert.Throws<ArgumentException>(() => {
			var sut = new DurationMaxTracker(
				metric: null,
				name: "the-tracker",
				expectedScrapeIntervalSeconds: 16,
				clock: _clock);
		});

		Assert.Equal(
			"ExpectedScrapeIntervalSeconds must be 0, 1, 5, 10 or a multiple of 15, but was 16",
			ex.Message);
	}

	[Fact]
	public void record_now_returns_now() {
		_clock.SecondsSinceEpoch = 500;
		var start = _clock.Now;
		_clock.SecondsSinceEpoch = 501;
		var end = _sut.RecordNow(start);
		var elapsedSeconds = end.ElapsedSecondsSince(start);
		Assert.Equal(1.000, elapsedSeconds);
	}

	[Fact]
	public void no_records() {
		AssertMeasurements(0);
	}

	[Fact]
	public void one_record() {
		AssertMeasurements(0);
		_clock.SecondsSinceEpoch = 500;
		var start = _clock.Now;
		_clock.SecondsSinceEpoch = 501;
		_sut.RecordNow(start);
		AssertMeasurements(1);
	}

	[Fact]
	public void two_records_ascending() {
		AssertMeasurements(0);
		_clock.SecondsSinceEpoch = 500;
		var start1 = _clock.Now;
		var start2 = _clock.Now;
		_clock.SecondsSinceEpoch = 501;
		_sut.RecordNow(start1); // record a 1s duration
		AssertMeasurements(1);
		_clock.SecondsSinceEpoch = 502;
		_sut.RecordNow(start2); // record a 2s duration
		AssertMeasurements(2);
	}

	[Fact]
	public void two_records_descending() {
		AssertMeasurements(0);
		_clock.SecondsSinceEpoch = 500;
		var start2 = _clock.Now;
		_clock.SecondsSinceEpoch = 502;
		_sut.RecordNow(start2); // record a 2s duration
		var start1 = _clock.Now;
		AssertMeasurements(2);
		_clock.SecondsSinceEpoch = 503;
		_sut.RecordNow(start1); // record a 1s duration
		AssertMeasurements(2);
	}

	[Fact]
	public void removes_stale_data() {
		_clock.SecondsSinceEpoch = 500;
		var start = _clock.Now;
		_clock.SecondsSinceEpoch = 501;
		_sut.RecordNow(start);
		_clock.SecondsSinceEpoch = 510;
		AssertMeasurements(1);
		_clock.SecondsSinceEpoch = 522;
		AssertMeasurements(0);
	}

	[Fact]
	public void removes_stale_data_incrementally() {
		_clock.SecondsSinceEpoch = 500;
		var start10 = _clock.Now;

		_clock.SecondsSinceEpoch = 505;
		var start9 = _clock.Now;

		_clock.SecondsSinceEpoch = 510;
		var start8 = _clock.Now;

		_clock.SecondsSinceEpoch = 515;
		var start7 = _clock.Now;

		_clock.SecondsSinceEpoch = 520;
		var start6 = _clock.Now;

		_clock.SecondsSinceEpoch = 525;
		var start5 = _clock.Now;

		// record a series of durations, each 4s apart (so in a different bucket)
		// there are 5 buckets
		AssertMeasurements(0);

		_clock.SecondsSinceEpoch = 510;
		_sut.RecordNow(start10); // record a 10s duration
		AssertMeasurements(10);

		_clock.SecondsSinceEpoch = 514;
		_sut.RecordNow(start9); // record a 9s duration
		AssertMeasurements(10);

		_clock.SecondsSinceEpoch = 518;
		_sut.RecordNow(start8); // record a 8s duration
		AssertMeasurements(10);

		_clock.SecondsSinceEpoch = 522;
		_sut.RecordNow(start7); // record a 7s duration
		AssertMeasurements(10);

		_clock.SecondsSinceEpoch = 526;
		_sut.RecordNow(start6); // record a 6s duration
		AssertMeasurements(10);

		_clock.SecondsSinceEpoch = 530;
		_sut.RecordNow(start5); // record a 5s duration
		// original measurement has become stale
		AssertMeasurements(9);
	}

	void AssertMeasurements(double expectedValue) {
		_listener.Observe();

		Assert.Collection(
			_listener.RetrieveMeasurements("the-metric"),
			m => {
				Assert.Equal(expectedValue, m.Value);
				Assert.Collection(
					m.Tags.ToArray(),
					t => {
						Assert.Equal("name", t.Key);
						Assert.Equal("the-tracker", t.Value);
					},
					t => {
						Assert.Equal("range", t.Key);
						Assert.Equal("16-20 seconds", t.Value);
					});
			});
	}

	public class BucketCalculatorTests {
		private readonly DurationMaxTracker.BucketCalculator _sut = new();

		[Theory]
		[InlineData(0, 1, 1, 0, 1)]
		[InlineData(1, 3, 1, 2, 3)]
		[InlineData(5, 4, 2, 6, 8)]
		[InlineData(10, 5, 3, 12, 15)]
		[InlineData(15, 5, 4, 16, 20)]
		[InlineData(30, 5, 8, 32, 40)]
		[InlineData(45, 5, 12, 48, 60)]
		[InlineData(60, 5, 16, 64, 80)]
		[InlineData(75, 5, 20, 80, 100)]
		[InlineData(90, 5, 24, 96, 120)]
		[InlineData(105, 5, 28, 112, 140)]
		[InlineData(120, 5, 32, 128, 160)]
		public void calculates_happy_path(
			int scrapeIntervalSeconds,
			int expectedNumBuckets,
			int expectedSecondsPerBucket,
			int expectedMinPeriodSeconds,
			int expectedMaxPeriodSeconds) {

			_sut.Calculate(
				scrapeIntervalSeconds,
				out var actualNumBuckets,
				out var actualSecondsPerBucket,
				out var actualMinPeriodSeconds,
				out var actualMaxPeriodSeconds);

			Assert.Equal(expectedNumBuckets, actualNumBuckets);
			Assert.Equal(expectedSecondsPerBucket, actualSecondsPerBucket);
			Assert.Equal(expectedMinPeriodSeconds, actualMinPeriodSeconds);
			Assert.Equal(expectedMaxPeriodSeconds, actualMaxPeriodSeconds);
		}

		[Fact]
		public void throws() {
			var ex = Assert.Throws<ArgumentException>(() => {
				_sut.Calculate(16, out _, out _, out _, out _);
			});

			Assert.Equal(
				"ExpectedScrapeIntervalSeconds must be 0, 1, 5, 10 or a multiple of 15, but was 16",
				ex.Message);
		}
	}
}
