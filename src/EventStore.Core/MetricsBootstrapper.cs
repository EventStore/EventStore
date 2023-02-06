using System;
using System.Diagnostics.Metrics;
using System.Linq;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.Index;
using EventStore.Core.Messaging;
using EventStore.Core.Services.VNode;
using EventStore.Core.Telemetry;
using EventStore.Core.TransactionLog.Scavenging;
using Conf = EventStore.Common.Configuration.TelemetryConfiguration;

namespace EventStore.Core;

public class Trackers {
	public IIndexStatusTracker IndexStatusTracker { get; set; } = new IndexStatusTracker.NoOp();
	public INodeStatusTracker NodeStatusTracker { get; set; } = new NodeStatusTracker.NoOp();
	public IScavengeStatusTracker ScavengeStatusTracker { get; set; } = new ScavengeStatusTracker.NoOp();
	public GrpcTrackers GrpcTrackers { get; } = new();
	public QueueTrackers QueueTrackers { get; set; } = new();
}

public class GrpcTrackers {
	private readonly IDurationTracker[] _trackers;

	public GrpcTrackers() {
		_trackers = new IDurationTracker[Enum.GetValues<Conf.GrpcMethod>().Cast<int>().Max() + 1];
		var noOp = new DurationTracker.NoOp();
		for (var i = 0; i < _trackers.Length; i++)
			_trackers[i] = noOp;
	}

	public IDurationTracker this[Conf.GrpcMethod index] {
		get => _trackers[(int)index];
		set => _trackers[(int)index] = value;
	}
}

public static class MetricsBootstrapper {
	public static void Bootstrap(
		Conf conf,
		TFChunkDbConfig dbConfig,
		Trackers trackers) {

		MessageLabelConfigurator.ConfigureMessageLabels(
			conf.MessageTypes, MessageHierarchy.MsgTypeIdByType.Keys);

		if (conf.ExpectedScrapeIntervalSeconds <= 0)
			return;

		var coreMeter = new Meter("EventStore.Core", version: "0.0.1");
		var statusMetric = new StatusMetric(coreMeter, "eventstore-statuses");
		var durationMetric = new DurationMetric(coreMeter, "eventstore-duration");
		var durationMaxMetric = new DurationMaxMetric(coreMeter, "eventstore-duration-max");
		var queueProcessingDurationMetric = new DurationMetric(coreMeter, "eventstore-queue-processing-duration");

		// checkpoints
		_ = new CheckpointMetric(
			coreMeter,
			"eventstore-checkpoints",
			conf.Checkpoints.Select(x => x switch {
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
		if (conf.StatusTrackers.Length > 0) {
			if (conf.StatusTrackers.Contains(Conf.StatusTracker.Index))
				trackers.IndexStatusTracker = new IndexStatusTracker(statusMetric);
			if (conf.StatusTrackers.Contains(Conf.StatusTracker.Node))
				trackers.NodeStatusTracker = new NodeStatusTracker(statusMetric);
			if (conf.StatusTrackers.Contains(Conf.StatusTracker.Scavenge))
				trackers.ScavengeStatusTracker = new ScavengeStatusTracker(statusMetric);
		}

		// grpc historgrams
		foreach (var method in Enum.GetValues<Conf.GrpcMethod>()) {
			if (conf.GrpcMethods.TryGetValue(method, out var label) && !string.IsNullOrWhiteSpace(label))
				trackers.GrpcTrackers[method] = new DurationTracker(durationMetric, label);
		}

		// queue length trackers
		trackers.QueueTrackers = new QueueTrackers(
			conf.Queues,
			name => new QueueTracker(
				name: name,
				new DurationMaxTracker(
					name: name,
					metric: durationMaxMetric,
					expectedScrapeIntervalSeconds: conf.ExpectedScrapeIntervalSeconds),
				new QueueProcessingTracker(
					metric: queueProcessingDurationMetric,
					queueName: name)));
	}
}
