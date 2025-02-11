// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using EventStore.Core.Bus;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.Index;
using EventStore.Core.Metrics;
using EventStore.Core.Services.VNode;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Scavenging;
using Conf = EventStore.Common.Configuration.MetricsConfiguration;

namespace EventStore.Core;

public class Trackers {
	public IInaugurationStatusTracker InaugurationStatusTracker { get; set; } = new NodeStatusTracker.NoOp();
	public IIndexStatusTracker IndexStatusTracker { get; set; } = new IndexStatusTracker.NoOp();
	public INodeStatusTracker NodeStatusTracker { get; set; } = new NodeStatusTracker.NoOp();
	public IScavengeStatusTracker ScavengeStatusTracker { get; set; } = new ScavengeStatusTracker.NoOp();
	public GrpcTrackers GrpcTrackers { get; } = new();
	public QueueTrackers QueueTrackers { get; set; } = new();
	public GossipTrackers GossipTrackers { get; set; } = new ();
	public ITransactionFileTracker TransactionFileTracker { get; set; } = new TFChunkTracker.NoOp();
	public IIndexTracker IndexTracker { get; set; } = new IndexTracker.NoOp();
	public IMaxTracker<long> WriterFlushSizeTracker { get; set; } = new MaxTracker<long>.NoOp();
	public IDurationMaxTracker WriterFlushDurationTracker { get; set; } = new DurationMaxTracker.NoOp();
	public ICacheHitsMissesTracker CacheHitsMissesTracker { get; set; } = new CacheHitsMissesTracker.NoOp();
	public ICacheResourcesTracker CacheResourcesTracker { get; set; } = new CacheResourcesTracker.NoOp();
	public IElectionCounterTracker ElectionCounterTracker { get; set; } = new ElectionsCounterTracker.NoOp();
	public IPersistentSubscriptionTracker PersistentSubscriptionTracker { get; set; } =
		IPersistentSubscriptionTracker.NoOp;
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

public class GossipTrackers {
	public IDurationTracker PullFromPeer { get; set; } = new DurationTracker.NoOp();
	public IDurationTracker PushToPeer { get; set; } = new DurationTracker.NoOp();
	public IDurationTracker ProcessingPushFromPeer { get; set; } = new DurationTracker.NoOp();
	public IDurationTracker ProcessingRequestFromPeer { get; set; } = new DurationTracker.NoOp();
	public IDurationTracker ProcessingRequestFromHttpClient { get; set; } = new DurationTracker.NoOp();
	public IDurationTracker ProcessingRequestFromGrpcClient { get; set; } = new DurationTracker.NoOp();
}

public static class MetricsBootstrapper {
	public const string LogicalChunkReadDistributionName = "kurrentdb-logical-chunk-read-distribution";

	public static void Bootstrap(
		Conf conf,
		TFChunkDbConfig dbConfig,
		Trackers trackers) {

		OptionsFormatter.LogConfig("Metrics", conf);

		MessageLabelConfigurator.ConfigureMessageLabels(
			conf.MessageTypes, InMemoryBus.KnownMessageTypes);

		if (conf.ExpectedScrapeIntervalSeconds <= 0)
			return;

		var coreMeter = new Meter("KurrentDB.Core", version: "1.0.0");
		var statusMetric = new StatusMetric(coreMeter, "kurrentdb-statuses");
		var grpcMethodMetric = new DurationMetric(coreMeter, "kurrentdb-grpc-method-duration");
		var gossipLatencyMetric = new DurationMetric(coreMeter, "kurrentdb-gossip-latency");
		var gossipProcessingMetric = new DurationMetric(coreMeter, "kurrentdb-gossip-processing-duration");
		var queueQueueingDurationMaxMetric = new DurationMaxMetric(coreMeter, "kurrentdb-queue-queueing-duration-max");
		var queueProcessingDurationMetric = new DurationMetric(coreMeter, "kurrentdb-queue-processing-duration");
		var queueBusyMetric = new AverageMetric(coreMeter, "kurrentdb-queue-busy", "seconds", label => new("queue", label));
		var byteMetric = new CounterMetric(coreMeter, "kurrentdb-io-bytes", unit: "bytes");
		var eventMetric = new CounterMetric(coreMeter, "kurrentdb-io-events", unit: "events");
		var recordReadDurationMetric = new DurationMetric(coreMeter, "kurrentdb-io-record-read-duration");
		var electionsCounterMetric = new CounterMetric(coreMeter, "kurrentdb-elections-count", unit: "");

		// incoming grpc calls
		var enabledCalls = conf.IncomingGrpcCalls.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToArray();
		if (enabledCalls.Length > 0) {
			_ = new IncomingGrpcCallsMetric(
				coreMeter,
				"kurrentdb-current-incoming-grpc-calls",
				"kurrentdb-incoming-grpc-calls",
				enabledCalls);
		}

		// cache hits/misses
		var enabledCacheHitsMisses = conf.CacheHitsMisses.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToArray();
		if (enabledCacheHitsMisses.Length > 0) {
			var metric = new CacheHitsMissesMetric(coreMeter, enabledCacheHitsMisses, "kurrentdb-cache-hits-misses", new() {
				{ Conf.Cache.StreamInfo, "stream-info" },
				{ Conf.Cache.Chunk, "chunk" },
			});
			trackers.CacheHitsMissesTracker = new CacheHitsMissesTracker(metric);
		}

		// dynamic cache resources
		if (conf.CacheResources) {
			var metrics = new CacheResourcesMetrics(coreMeter, "kurrentdb-cache-resources");
			trackers.CacheResourcesTracker = new CacheResourcesTracker(metrics);
		}

		// elections count
		if (conf.ElectionsCount) {
			trackers.ElectionCounterTracker = new ElectionsCounterTracker(new CounterSubMetric(electionsCounterMetric, []));
		}

		// events
		if (conf.Events.TryGetValue(Conf.EventTracker.Read, out var readEnabled) && readEnabled) {
			var readTag = new KeyValuePair<string, object>("activity", "read");
			trackers.TransactionFileTracker = new TFChunkTracker(
				readDistribution: new LogicalChunkReadDistributionMetric(
					meter: coreMeter,
					name: LogicalChunkReadDistributionName,
					writer: dbConfig.WriterCheckpoint,
					chunkSize: dbConfig.ChunkSize),
				readDurationMetric: recordReadDurationMetric,
				readBytes: new CounterSubMetric(byteMetric, [readTag]),
				readEvents: new CounterSubMetric(eventMetric, [readTag]));
		}

		// from a users perspective an event is written when it is indexed: thats when it can be read.
		if (conf.Events.TryGetValue(Conf.EventTracker.Written, out var writtenEnabled) && writtenEnabled) {
			trackers.IndexTracker = new IndexTracker(new CounterSubMetric(
				eventMetric,
				new[] {new KeyValuePair<string, object>("activity", "written")}));
		}

		// gossip
		if (conf.Gossip.Count != 0) {
			if (conf.Gossip.TryGetValue(Conf.GossipTracker.PullFromPeer, out var pullFromPeer) && pullFromPeer)
				trackers.GossipTrackers.PullFromPeer = new DurationTracker(gossipLatencyMetric, "pull-from-peer");

			if (conf.Gossip.TryGetValue(Conf.GossipTracker.PushToPeer, out var pushToPeer) && pushToPeer)
				trackers.GossipTrackers.PushToPeer = new DurationTracker(gossipLatencyMetric, "push-to-peer");

			if (conf.Gossip.TryGetValue(Conf.GossipTracker.ProcessingPushFromPeer, out var processingPushFromPeer) && processingPushFromPeer)
				trackers.GossipTrackers.ProcessingPushFromPeer = new DurationTracker(gossipProcessingMetric, "push-from-peer");

			if (conf.Gossip.TryGetValue(Conf.GossipTracker.ProcessingRequestFromPeer, out var processingRequestFromPeer) && processingRequestFromPeer)
				trackers.GossipTrackers.ProcessingRequestFromPeer = new DurationTracker(gossipProcessingMetric, "request-from-peer");

			if (conf.Gossip.TryGetValue(Conf.GossipTracker.ProcessingRequestFromGrpcClient, out var processingRequestFromGrpcClient) && processingRequestFromGrpcClient)
				trackers.GossipTrackers.ProcessingRequestFromGrpcClient = new DurationTracker(gossipProcessingMetric, "request-from-grpc-client");

			if (conf.Gossip.TryGetValue(Conf.GossipTracker.ProcessingRequestFromHttpClient, out var processingRequestFromHttpClient) && processingRequestFromHttpClient)
				trackers.GossipTrackers.ProcessingRequestFromHttpClient = new DurationTracker(gossipProcessingMetric, "request-from-http-client");
		}

		// persistent subscriptions
		if (conf.PersistentSubscriptionStats) {
			var tracker = new PersistentSubscriptionTracker();
			trackers.PersistentSubscriptionTracker = tracker;

			coreMeter.CreateObservableUpDownCounter("kurrentdb-persistent-sub-connections", tracker.ObserveConnectionsCount);
			coreMeter.CreateObservableUpDownCounter("kurrentdb-persistent-sub-parked-messages", tracker.ObserveParkedMessages);
			coreMeter.CreateObservableUpDownCounter("kurrentdb-persistent-sub-in-flight-messages", tracker.ObserveInFlightMessages);
			coreMeter.CreateObservableUpDownCounter("kurrentdb-persistent-sub-oldest-parked-message-seconds", tracker.ObserveOldestParkedMessage);

			// these only go up, but are not strictly counters; should not have `_total` appended
			coreMeter.CreateObservableUpDownCounter("kurrentdb-persistent-sub-last-known-event-number", tracker.ObserveLastKnownEvent);
			coreMeter.CreateObservableUpDownCounter("kurrentdb-persistent-sub-last-known-event-commit-position", tracker.ObserveLastKnownEventCommitPosition);
			coreMeter.CreateObservableUpDownCounter("kurrentdb-persistent-sub-checkpointed-event-number", tracker.ObserveLastCheckpointedEvent);
			coreMeter.CreateObservableUpDownCounter("kurrentdb-persistent-sub-checkpointed-event-commit-position", tracker.ObserveLastCheckpointedEventCommitPosition);

			coreMeter.CreateObservableCounter("kurrentdb-persistent-sub-items-processed", tracker.ObserveItemsProcessed);
		}

		// checkpoints
		_ = new CheckpointMetric(
			coreMeter,
			"kurrentdb-checkpoints",
			conf.Checkpoints.Where(x => x.Value).Select(x => x.Key switch {
				Conf.Checkpoint.Chaser => dbConfig.ChaserCheckpoint,
				Conf.Checkpoint.Epoch => dbConfig.EpochCheckpoint,
				Conf.Checkpoint.Index => dbConfig.IndexCheckpoint,
				Conf.Checkpoint.Proposal => dbConfig.ProposalCheckpoint,
				Conf.Checkpoint.Replication => dbConfig.ReplicationCheckpoint,
				Conf.Checkpoint.StreamExistenceFilter => dbConfig.StreamExistenceFilterCheckpoint,
				Conf.Checkpoint.Truncate => dbConfig.TruncateCheckpoint,
				Conf.Checkpoint.Writer => dbConfig.WriterCheckpoint,
				_ => throw new Exception(
					$"Unknown checkpoint in MetricsConfiguration. Valid values are " +
					$"{string.Join(", ", Enum.GetValues<Conf.Checkpoint>())}"),
			}).ToArray());

		// status metrics
		if (conf.Statuses.Count > 0) {
			if (conf.Statuses.TryGetValue(Conf.StatusTracker.Node, out var nodeStatus) && nodeStatus) {
				var tracker = new NodeStatusTracker(statusMetric);
				trackers.NodeStatusTracker = tracker;
				trackers.InaugurationStatusTracker = tracker;
			}

			if (conf.Statuses.TryGetValue(Conf.StatusTracker.Index, out var indexStatus) && indexStatus)
				trackers.IndexStatusTracker = new IndexStatusTracker(statusMetric);

			if (conf.Statuses.TryGetValue(Conf.StatusTracker.Scavenge, out var scavengeStatus) && scavengeStatus)
				trackers.ScavengeStatusTracker = new ScavengeStatusTracker(statusMetric);
		}

		// grpc historgrams
		foreach (var method in Enum.GetValues<Conf.GrpcMethod>()) {
			if (conf.GrpcMethods.TryGetValue(method, out var label) && !string.IsNullOrWhiteSpace(label))
				trackers.GrpcTrackers[method] = new DurationTracker(grpcMethodMetric, label);
		}

		// storage writer
		if (conf.Writer.Count > 0) {
			if (conf.Writer.TryGetValue(Conf.WriterTracker.FlushSize, out var flushSizeEnabled) && flushSizeEnabled) {
				var maxMetric = new MaxMetric<long>(coreMeter, "kurrentdb-writer-flush-size-max");
				trackers.WriterFlushSizeTracker = new MaxTracker<long>(
					metric: maxMetric,
					name: null,
					expectedScrapeIntervalSeconds: conf.ExpectedScrapeIntervalSeconds);
			}

			if (conf.Writer.TryGetValue(Conf.WriterTracker.FlushDuration, out var flushDurationEnabled) && flushDurationEnabled) {
				var maxDurationmetric = new DurationMaxMetric(coreMeter, "kurrentdb-writer-flush-duration-max");
				trackers.WriterFlushDurationTracker = new DurationMaxTracker(
					maxDurationmetric,
					name: null,
					expectedScrapeIntervalSeconds: conf.ExpectedScrapeIntervalSeconds);
			}
		}

		// queue trackers
		Func<string, IQueueBusyTracker> busyTrackerFactory = name => new QueueBusyTracker.NoOp();
		Func<string, IDurationMaxTracker> lengthFactory = name => new DurationMaxTracker.NoOp();
		Func<string, IQueueProcessingTracker> processingFactory = name => new QueueProcessingTracker.NoOp();

		if (conf.Queues.TryGetValue(Conf.QueueTracker.Busy, out var busyEnabled) && busyEnabled)
			busyTrackerFactory = name => new QueueBusyTracker(queueBusyMetric, name);

		if (conf.Queues.TryGetValue(Conf.QueueTracker.Length, out var lengthEnabled) && lengthEnabled)
			lengthFactory = name => new DurationMaxTracker(
				name: name,
				metric: queueQueueingDurationMaxMetric,
				expectedScrapeIntervalSeconds: conf.ExpectedScrapeIntervalSeconds);

		if (conf.Queues.TryGetValue(Conf.QueueTracker.Processing, out var processingEnabled) && processingEnabled)
			processingFactory = name => new QueueProcessingTracker(queueProcessingDurationMetric, name);

		trackers.QueueTrackers = new QueueTrackers(conf.QueueLabels, busyTrackerFactory, lengthFactory, processingFactory);

		// kestrel
		if (conf.Kestrel.TryGetValue(Conf.KestrelTracker.ConnectionCount, out var kestrelConnections) && kestrelConnections) {
			_ = new ConnectionMetric(coreMeter, "kurrentdb-kestrel-connections");
		}

		var timeout = TimeSpan.FromSeconds(1);

		// system
		var systemMetrics = new SystemMetrics(coreMeter, timeout, conf.System);
		systemMetrics.CreateLoadAverageMetric("kurrentdb-sys-load-avg", new() {
			{ Conf.SystemTracker.LoadAverage1m, "1m" },
			{ Conf.SystemTracker.LoadAverage5m, "5m" },
			{ Conf.SystemTracker.LoadAverage15m, "15m" },
		});

		systemMetrics.CreateCpuMetric("kurrentdb-sys-cpu");

		systemMetrics.CreateMemoryMetric("kurrentdb-sys-mem", new() {
			{ Conf.SystemTracker.FreeMem, "free" },
			{ Conf.SystemTracker.TotalMem, "total" },
		});

		systemMetrics.CreateDiskMetric("kurrentdb-sys-disk", dbConfig.Path, new() {
			{ Conf.SystemTracker.DriveTotalBytes, "total" },
			{ Conf.SystemTracker.DriveUsedBytes, "used" },
		});

		// process
		var processMetrics = new ProcessMetrics(coreMeter, timeout, conf.ExpectedScrapeIntervalSeconds, conf.Process);
		processMetrics.CreateObservableMetrics(new() {
			{ Conf.ProcessTracker.UpTime, "kurrentdb-proc-up-time" },
			{ Conf.ProcessTracker.Cpu, "kurrentdb-proc-cpu" },
			{ Conf.ProcessTracker.ThreadCount, "kurrentdb-proc-thread-count" },
			{ Conf.ProcessTracker.ThreadPoolPendingWorkItemCount, "kurrentdb-proc-thread-pool-pending-work-item-count" },
			{ Conf.ProcessTracker.LockContentionCount, "kurrentdb-proc-contention-count" },
			{ Conf.ProcessTracker.ExceptionCount, "kurrentdb-proc-exception-count" },
			{ Conf.ProcessTracker.TimeInGc, "kurrentdb-gc-time-in-gc" },
			{ Conf.ProcessTracker.HeapSize, "kurrentdb-gc-heap-size" },
			{ Conf.ProcessTracker.HeapFragmentation, "kurrentdb-gc-heap-fragmentation" },
			{ Conf.ProcessTracker.TotalAllocatedBytes, "kurrentdb-gc-allocated" },
			{ Conf.ProcessTracker.GcPauseDuration, "kurrentdb-gc-pause-duration-max" },
		});

		processMetrics.CreateMemoryMetric("kurrentdb-proc-mem", new() {
			{ Conf.ProcessTracker.MemWorkingSet, "working-set" },
			{ Conf.ProcessTracker.MemPagedBytes, "paged-bytes" },
			{ Conf.ProcessTracker.MemVirtualBytes, "virtual-bytes" },
		});

		processMetrics.CreateGcGenerationSizeMetric("kurrentdb-gc-generation-size", new() {
			{ Conf.ProcessTracker.Gen0Size, "gen0" },
			{ Conf.ProcessTracker.Gen1Size, "gen1" },
			{ Conf.ProcessTracker.Gen2Size, "gen2" },
			{ Conf.ProcessTracker.LohSize, "loh" },
		});

		processMetrics.CreateGcCollectionCountMetric("kurrentdb-gc-collection-count", new() {
			{ Conf.ProcessTracker.Gen0CollectionCount, "gen0" },
			{ Conf.ProcessTracker.Gen1CollectionCount, "gen1" },
			{ Conf.ProcessTracker.Gen2CollectionCount, "gen2" },
		});

		processMetrics.CreateDiskBytesMetric("kurrentdb-disk-io-bytes", new() {
			{ Conf.ProcessTracker.DiskReadBytes, "read" },
			{ Conf.ProcessTracker.DiskWrittenBytes, "written" },
		});

		processMetrics.CreateDiskOpsMetric("kurrentdb-disk-io-operations", new() {
			{ Conf.ProcessTracker.DiskReadOps, "read" },
			{ Conf.ProcessTracker.DiskWrittenOps, "written" },
		});
	}
}
