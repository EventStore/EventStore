using System.Diagnostics.Metrics;
using System.Linq;
using EventStore.Core.TransactionLog.Checkpoint;
using Xunit;

namespace EventStore.Core.XUnit.Tests.TransactionLog.Checkpoint {
	public class CheckpointMetricTests {
		[Fact]
		public void can_collect() {
			var metric = new CheckpointMetric(
				new Meter("Eventstore.Core.XUnit.Tests"),
				"eventstore-checkpoints",
				new InMemoryCheckpoint("checkpoint", 5));

			Assert.Collection(
				metric.Observe().ToArray(),
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
