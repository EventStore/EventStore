using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using EventStore.Core.Index;
using EventStore.Core.Telemetry;
using EventStore.Core.XUnit.Tests.Telemetry;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Index {
	public class IndexStatusTrackerTests {
		private readonly FakeClock _clock = new();
		private readonly StatusMetric _metric;
		private readonly IndexStatusTracker _sut;

		public IndexStatusTrackerTests() {
			_metric = new StatusMetric(
				new Meter($"Eventstore.Core.XUnit.Tests.{nameof(IndexStatusTrackerTests)}"),
				"eventstore-statuses",
				_clock);
			_sut = new IndexStatusTracker(_metric);
		}

		[Fact]
		public void can_observe_merging() {
			_clock.SecondsSinceEpoch = 500;
			AssertMeasurements("Idle", 500, _metric.Observe());

			using (_sut.StartMerging()) {
				_clock.SecondsSinceEpoch = 501;
				AssertMeasurements("Merging", 501, _metric.Observe());
			}

			_clock.SecondsSinceEpoch = 502;
			AssertMeasurements("Idle", 502, _metric.Observe());
		}

		[Fact]
		public void can_observe_scavenging() {
			_clock.SecondsSinceEpoch = 500;
			AssertMeasurements("Idle", 500, _metric.Observe());

			using (_sut.StartScavenging()) {
				_clock.SecondsSinceEpoch = 501;
				AssertMeasurements("Scavenging", 501, _metric.Observe());
			}

			_clock.SecondsSinceEpoch = 502;
			AssertMeasurements("Idle", 502, _metric.Observe());
		}

		static void AssertMeasurements(
			string expectedStatus,
			int expectedValue,
			IEnumerable<Measurement<long>> measurements) {

			Assert.Collection(measurements.ToArray(),
				m => {
					Assert.Equal(expectedValue, m.Value);
					Assert.Collection(
						m.Tags.ToArray(),
						t => {
							Assert.Equal("name", t.Key);
							Assert.Equal("Index", t.Value);
						},
						t => {
							Assert.Equal("status", t.Key);
							Assert.Equal(expectedStatus, t.Value);
						});
				});
		}
	}
}
