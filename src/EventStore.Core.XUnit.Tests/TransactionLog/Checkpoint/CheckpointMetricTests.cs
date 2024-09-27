// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Diagnostics.Metrics;
using System.Linq;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.XUnit.Tests.Metrics;
using Xunit;

namespace EventStore.Core.XUnit.Tests.TransactionLog.Checkpoint {
	public class CheckpointMetricTests {
		[Fact]
		public void can_collect() {
			using var meter = new Meter($"{typeof(CheckpointMetricTests)}");
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
