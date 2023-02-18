using EventStore.Core.Telemetry;
using EventStore.Core.Time;
using Xunit;
using Conf = EventStore.Common.Configuration.TelemetryConfiguration;

namespace EventStore.Core.XUnit.Tests.Telemetry {
	public class QueueTrackersTests {
		[Fact]
		public void default_trackers_yields_noop_tracker() {
			var sut = new QueueTrackers();
			var tracker = sut.GetTrackerForQueue("MainQueue");
			Assert.Equal("NoOp", tracker.Name);
		}

		[Fact]
		public void not_matched_yields_noop_tracker() {
			var sut = GenSut(
				new Conf.LabelMappingCase {
					Regex = "MainQueue",
					Label = "MainQueue",
				});


			var tracker = sut.GetTrackerForQueue("MainQueue");
			Assert.Equal("MainQueue", tracker.Name);

			tracker = sut.GetTrackerForQueue("WriterQueue");
			Assert.Equal("NoOp", tracker.Name);
		}

		[Fact]
		public void empty_tracker_yields_no_op() {
			var sut = GenSut(
				new Conf.LabelMappingCase {
					Regex = "MainQueue",
					Label = "",
				},
				new Conf.LabelMappingCase {
					Regex = "WriterQueue",
					Label = "WriterQueue",
				});


			var tracker = sut.GetTrackerForQueue("WriterQueue");
			Assert.Equal("WriterQueue", tracker.Name);

			tracker = sut.GetTrackerForQueue("MainQueue");
			Assert.Equal("NoOp", tracker.Name);
		}

		[Fact]
		public void patterns_applied_in_order() {
			var sut = GenSut(
				new Conf.LabelMappingCase {
					Regex = "MainQueue",
					Label = "MainQueue",
				},
				new Conf.LabelMappingCase {
					Regex = ".*",
					Label = "OtherQueue",
				});

			var tracker = sut.GetTrackerForQueue("MainQueue");
			Assert.Equal("MainQueue", tracker.Name);

			tracker = sut.GetTrackerForQueue("WriterQueue");
			Assert.Equal("OtherQueue", tracker.Name);
		}

		[Fact]
		public void matches_groups() {
			var sut = GenSut(
				new Conf.LabelMappingCase {
					Regex = "Worker #(.*)",
					Label = "Worker Queue $1",
				});

			var tracker = sut.GetTrackerForQueue("Worker #3");
			Assert.Equal("Worker Queue 3", tracker.Name);
		}

		QueueTrackers GenSut(params Conf.LabelMappingCase[] map) =>
			new(map, x => {
				var tracker = new FakeTracker { Name = x };
				return new QueueTracker(x, tracker, tracker);
			});

		class FakeTracker : IDurationMaxTracker, IQueueProcessingTracker {
			public string Name { get; init; }

			public Instant RecordNow(Instant start) => start;

			public Instant RecordNow(Instant start, string messageType) => start;
		}
	}
}
