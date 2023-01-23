using System.Diagnostics.Metrics;
using System.Linq;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.XUnit.Tests.Telemetry;
using Xunit;

namespace EventStore.Core.XUnit.Tests.TransactionLog.Checkpoint {
	public class CheckpointMetricTests {
		[Fact]
		public void can_collect() {
			var meter = new Meter($"{typeof(CheckpointMetricTests)}");
			using var listener = new TestMeterListener<long>(meter);
			var metric = new CheckpointMetric(
				meter,
				"eventstore-checkpoints",
				new InMemoryCheckpoint("checkpoint", 5));

			listener.Observe();
			Assert.Collection(
				listener.RetrieveMeasurements("eventstore-checkpoints"),
				measurement => {
					Assert.Equal(5, measurement.Value);
					Assert.Collection(
						measurement.Tags.ToArray(),
						tag => {
							Assert.Equal("name", tag.Key);
							Assert.Equal("checkpoint", tag.Value);
						},
						tag => {
							Assert.Equal("read", tag.Key);
							Assert.Equal("non-flushed", tag.Value);
						});
				});
		}
	}
}
