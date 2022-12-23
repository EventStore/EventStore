using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using EventStore.Core.Telemetry;
using EventStore.Core.TransactionLog.Scavenging;
using EventStore.Core.XUnit.Tests.Telemetry;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Scavenge {
	public class ScavengeStatusTrackerTests {
		private readonly FakeClock _clock = new();
		private readonly StatusMetric _metric;
		private readonly ScavengeStatusTracker _sut;

		public ScavengeStatusTrackerTests() {
			_metric = new StatusMetric(
				new Meter($"Eventstore.Core.XUnit.Tests.{nameof(ScavengeStatusTrackerTests)}"),
				"eventstore-statuses",
				_clock);
			_sut = new ScavengeStatusTracker(_metric);
		}

		[Fact]
		public void can_observe_activity() {
			_clock.SecondsSinceEpoch = 500;
			AssertMeasurements("Idle", 500, _metric.Observe());

			using (_sut.StartActivity("Accumulation")) {
				_clock.SecondsSinceEpoch = 501;
				AssertMeasurements("Accumulation Phase", 501, _metric.Observe());
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
							Assert.Equal("Scavenge", t.Value);
						},
						t => {
							Assert.Equal("status", t.Key);
							Assert.Equal(expectedStatus, t.Value);
						});
				});
		}
	}
}
