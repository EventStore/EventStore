using System;
using System.Linq;
using EventStore.Common.Configuration;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Checkpoint;
using System.Diagnostics.Metrics;

namespace EventStore.Core;

public static class MetricsBootstrapper {
	public static void Bootstrap(
		TelemetryConfiguration telemetryConfiguration,
		TFChunkDbConfig dbConfig) {

		var coreMeter = new Meter("EventStore.Core");

		_ = new CheckpointMetric(
			coreMeter,
			"eventstore-checkpoints",
			telemetryConfiguration.Checkpoints.Select(x => x switch {
				TelemetryConfiguration.Checkpoint.Chaser => dbConfig.ChaserCheckpoint,
				TelemetryConfiguration.Checkpoint.Epoch => dbConfig.EpochCheckpoint,
				TelemetryConfiguration.Checkpoint.Index => dbConfig.IndexCheckpoint,
				TelemetryConfiguration.Checkpoint.Proposal => dbConfig.ProposalCheckpoint,
				TelemetryConfiguration.Checkpoint.Replication => dbConfig.ReplicationCheckpoint,
				TelemetryConfiguration.Checkpoint.StreamExistenceFilter => dbConfig.StreamExistenceFilterCheckpoint,
				TelemetryConfiguration.Checkpoint.Truncate => dbConfig.TruncateCheckpoint,
				TelemetryConfiguration.Checkpoint.Writer => dbConfig.WriterCheckpoint,
				_ => throw new Exception(
					$"Unknown checkpoint in TelemetryConfiguration. Valid values are " +
					$"{string.Join(", ", Enum.GetValues<TelemetryConfiguration.Checkpoint>())}"),
			}).ToArray());
	}
}
