using System.Diagnostics.Metrics;
using System.Linq;
using EventStore.Core.Services.VNode;
using EventStore.Core.Telemetry;
using EventStore.Core.XUnit.Tests.Telemetry;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Services.VNode {
	public class InaugurationStatusTrackerTests {
		private readonly FakeClock _clock = new();
		private readonly InaugurationStatusTracker _sut;
		private readonly TestMeterListener<long> _listener;

		public InaugurationStatusTrackerTests() {
			var meter = new Meter($"{typeof(InaugurationStatusTrackerTests)}");
			var metric = new StatusMetric(
				meter,
				"eventstore-statuses",
				_clock);
			_listener = new TestMeterListener<long>(meter);
			_sut = new InaugurationStatusTracker(metric);
		}

		[Fact]
		public void can_observe_initial() {
			_clock.SecondsSinceEpoch = 500;
			_sut.OnStateChange(InaugurationManager.ManagerState.BecomingLeader);
			AssertMeasurements("BecomingLeader", 500);

			_clock.SecondsSinceEpoch = 502;
			_sut.OnStateChange(InaugurationManager.ManagerState.Idle);
			AssertMeasurements("Idle", 502);
		}

		[Fact]
		public void can_observe_waiting_for_chaser() {
			_clock.SecondsSinceEpoch = 500;
			AssertMeasurements("Idle", 500);

			_clock.SecondsSinceEpoch = 502;
			_sut.OnStateChange(InaugurationManager.ManagerState.WaitingForChaser);
			AssertMeasurements("WaitingForChaser", 502);
		}

		[Fact]
		public void can_observe_writing_epoch() {
			_clock.SecondsSinceEpoch = 500;
			AssertMeasurements("Idle", 500);

			_clock.SecondsSinceEpoch = 502;
			_sut.OnStateChange(InaugurationManager.ManagerState.WritingEpoch);
			AssertMeasurements("WritingEpoch", 502);
		}

		[Fact]
		public void can_observe_waiting_for_conditions() {
			_clock.SecondsSinceEpoch = 500;
			AssertMeasurements("Idle", 500);

			_clock.SecondsSinceEpoch = 502;
			_sut.OnStateChange(InaugurationManager.ManagerState.WaitingForConditions);
			AssertMeasurements("WaitingForConditions", 502);
		}

		[Fact]
		public void can_observe_becoming_leader() {
			_clock.SecondsSinceEpoch = 500;
			AssertMeasurements("Idle", 500);

			_clock.SecondsSinceEpoch = 502;
			_sut.OnStateChange(InaugurationManager.ManagerState.BecomingLeader);
			AssertMeasurements("BecomingLeader", 502);
		}

		void AssertMeasurements(string expectedStatus, int expectedValue) {
			_listener.Observe();
			Assert.Collection(_listener.RetrieveMeasurements("eventstore-statuses").ToArray(),
				m => {
					Assert.Equal(expectedValue, m.Value);
					Assert.Collection(
						m.Tags.ToArray(),
						t => {
							Assert.Equal("name", t.Key);
							Assert.Equal("Inauguration", t.Value);
						},
						t => {
							Assert.Equal("status", t.Key);
							Assert.Equal(expectedStatus, t.Value);
						});
				});
		}
	}
}
