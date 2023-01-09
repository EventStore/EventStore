using System;
using System.Linq;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Checkpoint;
using System.Diagnostics.Metrics;
using EventStore.Core.Index;
using EventStore.Core.Services.VNode;
using EventStore.Core.Telemetry;
using EventStore.Core.TransactionLog.Scavenging;
using Conf = EventStore.Common.Configuration.TelemetryConfiguration;

namespace EventStore.Core;

public class Trackers {
	public IIndexStatusTracker IndexStatusTracker { get; set; } = new IndexStatusTracker.NoOp();
	public INodeStatusTracker NodeStatusTracker { get; set; } = new NodeStatusTracker.NoOp();
	public IScavengeStatusTracker ScavengeStatusTracker { get; set; } = new ScavengeStatusTracker.NoOp();
}

public static class MetricsBootstrapper {
	public static void Bootstrap(
		Conf telemetryConfiguration,
		TFChunkDbConfig dbConfig,
		Trackers trackers) {

		var coreMeter = new Meter("EventStore.Core");

		// checkpoints
		_ = new CheckpointMetric(
			coreMeter,
			"eventstore-checkpoints",
			telemetryConfiguration.Checkpoints.Select(x => x switch {
				Conf.Checkpoint.Chaser => dbConfig.ChaserCheckpoint,
				Conf.Checkpoint.Epoch => dbConfig.EpochCheckpoint,
				Conf.Checkpoint.Index => dbConfig.IndexCheckpoint,
				Conf.Checkpoint.Proposal => dbConfig.ProposalCheckpoint,
				Conf.Checkpoint.Replication => dbConfig.ReplicationCheckpoint,
				Conf.Checkpoint.StreamExistenceFilter => dbConfig.StreamExistenceFilterCheckpoint,
				Conf.Checkpoint.Truncate => dbConfig.TruncateCheckpoint,
				Conf.Checkpoint.Writer => dbConfig.WriterCheckpoint,
				_ => throw new Exception(
					$"Unknown checkpoint in TelemetryConfiguration. Valid values are " +
					$"{string.Join(", ", Enum.GetValues<Conf.Checkpoint>())}"),
			}).ToArray());

		// status metrics
		if (telemetryConfiguration.StatusTrackers.Length > 0) {
			var statusMetric = new StatusMetric(coreMeter, "eventstore-statuses");
			if (telemetryConfiguration.StatusTrackers.Contains(Conf.StatusTracker.Index))
				trackers.IndexStatusTracker = new IndexStatusTracker(statusMetric);
			if (telemetryConfiguration.StatusTrackers.Contains(Conf.StatusTracker.Node))
				trackers.NodeStatusTracker = new NodeStatusTracker(statusMetric);
			if (telemetryConfiguration.StatusTrackers.Contains(Conf.StatusTracker.Scavenge))
				trackers.ScavengeStatusTracker = new ScavengeStatusTracker(statusMetric);
		}
	}
}
