using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.Index;
using EventStore.Core.Messaging;
using EventStore.Core.Metrics;
using EventStore.Core.Services.VNode;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Scavenging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Serilog;
using Configuration = EventStore.Common.Configuration.MetricsConfiguration;

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
}

public class GrpcTrackers {
	private readonly IDurationTracker[] _trackers;

	public GrpcTrackers() {
		_trackers = new IDurationTracker[Enum.GetValues<Configuration.GrpcMethod>().Cast<int>().Max() + 1];
		var noOp = new DurationTracker.NoOp();
		for (var i = 0; i < _trackers.Length; i++)
			_trackers[i] = noOp;
	}

	public IDurationTracker this[Configuration.GrpcMethod index] {
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
	private static readonly ILogger Log = Serilog.Log.ForContext(typeof(MetricsBootstrapper));

	public static void Bootstrap(
		Configuration configuration,
		TFChunkDbConfig dbConfig,
		Trackers trackers) {

		LogConfig(configuration);

		EventStoreCore.MessageRegistry
			.Relabel(configuration.MessageTypes.Select(x => new MessageLabelMap(x.Regex, x.Label)).ToArray());
		
		if (configuration.ExpectedScrapeIntervalSeconds <= 0)
			return;

		var coreMeter = new Meter("EventStore.Core", version: "1.0.0");
		var statusMetric = new StatusMetric(coreMeter, "eventstore-statuses");
		var grpcMethodMetric = new DurationMetric(coreMeter, "eventstore-grpc-method-duration");
		var gossipLatencyMetric = new DurationMetric(coreMeter, "eventstore-gossip-latency");
		var gossipProcessingMetric = new DurationMetric(coreMeter, "eventstore-gossip-processing-duration");
		var queueQueueingDurationMaxMetric = new DurationMaxMetric(coreMeter, "eventstore-queue-queueing-duration-max");
		var queueProcessingDurationMetric = new DurationMetric(coreMeter, "eventstore-queue-processing-duration");
		var queueBusyMetric = new AverageMetric(coreMeter, "eventstore-queue-busy", "seconds", label => new("queue", label));
		var byteMetric = new CounterMetric(coreMeter, "eventstore-io", unit: "bytes");
		var eventMetric = new CounterMetric(coreMeter, "eventstore-io", unit: "events");

		// incoming grpc calls
		var enabledCalls = configuration.IncomingGrpcCalls.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToArray();
		if (enabledCalls.Length > 0) {
			_ = new IncomingGrpcCallsMetric(
				coreMeter,
				"eventstore-current-incoming-grpc-calls",
				"eventstore-incoming-grpc-calls",
				enabledCalls);
		}

		// cache hits/misses
		var enabledCacheHitsMisses = configuration.CacheHitsMisses.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToArray();
		if (enabledCacheHitsMisses.Length > 0) {
			var metric = new CacheHitsMissesMetric(coreMeter, enabledCacheHitsMisses, "eventstore-cache-hits-misses", new() {
				{ Configuration.Cache.StreamInfo, "stream-info" },
				{ Configuration.Cache.Chunk, "chunk" },
			});
			trackers.CacheHitsMissesTracker = new CacheHitsMissesTracker(metric);
		}

		// dynamic cache resources
		if (configuration.CacheResources) {
			var metrics = new CacheResourcesMetrics(coreMeter, "eventstore-cache-resources");
			trackers.CacheResourcesTracker = new CacheResourcesTracker(metrics);
		}

		// events
		if (configuration.Events.TryGetValue(Configuration.EventTracker.Read, out var readEnabled) && readEnabled) {
			var readTag = new KeyValuePair<string, object>("activity", "read");
			trackers.TransactionFileTracker = new TFChunkTracker(
				readBytes: new CounterSubMetric(byteMetric, new[] {readTag}),
				readEvents: new CounterSubMetric(eventMetric, new[] {readTag}));
		}

		// from a users perspective an event is written when it is indexed: thats when it can be read.
		if (configuration.Events.TryGetValue(Configuration.EventTracker.Written, out var writtenEnabled) && writtenEnabled) {
			trackers.IndexTracker = new IndexTracker(new CounterSubMetric(
				eventMetric,
				new[] {new KeyValuePair<string, object>("activity", "written")}));
		}

		// gossip
		if (configuration.Gossip.Count != 0) {
			if (configuration.Gossip.TryGetValue(Configuration.GossipTracker.PullFromPeer, out var pullFromPeer) && pullFromPeer)
				trackers.GossipTrackers.PullFromPeer = new DurationTracker(gossipLatencyMetric, "pull-from-peer");

			if (configuration.Gossip.TryGetValue(Configuration.GossipTracker.PushToPeer, out var pushToPeer) && pushToPeer)
				trackers.GossipTrackers.PushToPeer = new DurationTracker(gossipLatencyMetric, "push-to-peer");

			if (configuration.Gossip.TryGetValue(Configuration.GossipTracker.ProcessingPushFromPeer, out var processingPushFromPeer) && processingPushFromPeer)
				trackers.GossipTrackers.ProcessingPushFromPeer = new DurationTracker(gossipProcessingMetric, "push-from-peer");

			if (configuration.Gossip.TryGetValue(Configuration.GossipTracker.ProcessingRequestFromPeer, out var processingRequestFromPeer) && processingRequestFromPeer)
				trackers.GossipTrackers.ProcessingRequestFromPeer = new DurationTracker(gossipProcessingMetric, "request-from-peer");

			if (configuration.Gossip.TryGetValue(Configuration.GossipTracker.ProcessingRequestFromGrpcClient, out var processingRequestFromGrpcClient) && processingRequestFromGrpcClient)
				trackers.GossipTrackers.ProcessingRequestFromGrpcClient = new DurationTracker(gossipProcessingMetric, "request-from-grpc-client");

			if (configuration.Gossip.TryGetValue(Configuration.GossipTracker.ProcessingRequestFromHttpClient, out var processingRequestFromHttpClient) && processingRequestFromHttpClient)
				trackers.GossipTrackers.ProcessingRequestFromHttpClient = new DurationTracker(gossipProcessingMetric, "request-from-http-client");
		}

		// checkpoints
		_ = new CheckpointMetric(
			coreMeter,
			"eventstore-checkpoints",
			configuration.Checkpoints.Where(x => x.Value).Select(x => x.Key switch {
				Configuration.Checkpoint.Chaser => dbConfig.ChaserCheckpoint,
				Configuration.Checkpoint.Epoch => dbConfig.EpochCheckpoint,
				Configuration.Checkpoint.Index => dbConfig.IndexCheckpoint,
				Configuration.Checkpoint.Proposal => dbConfig.ProposalCheckpoint,
				Configuration.Checkpoint.Replication => dbConfig.ReplicationCheckpoint,
				Configuration.Checkpoint.StreamExistenceFilter => dbConfig.StreamExistenceFilterCheckpoint,
				Configuration.Checkpoint.Truncate => dbConfig.TruncateCheckpoint,
				Configuration.Checkpoint.Writer => dbConfig.WriterCheckpoint,
				_ => throw new Exception(
					$"Unknown checkpoint in MetricsConfiguration. Valid values are " +
					$"{string.Join(", ", Enum.GetValues<Configuration.Checkpoint>())}"),
			}).ToArray());

		// status metrics
		if (configuration.Statuses.Count > 0) {
			if (configuration.Statuses.TryGetValue(Configuration.StatusTracker.Node, out var nodeStatus) && nodeStatus) {
				var tracker = new NodeStatusTracker(statusMetric);
				trackers.NodeStatusTracker = tracker;
				trackers.InaugurationStatusTracker = tracker;
			}

			if (configuration.Statuses.TryGetValue(Configuration.StatusTracker.Index, out var indexStatus) && indexStatus)
				trackers.IndexStatusTracker = new IndexStatusTracker(statusMetric);

			if (configuration.Statuses.TryGetValue(Configuration.StatusTracker.Scavenge, out var scavengeStatus) && scavengeStatus)
				trackers.ScavengeStatusTracker = new ScavengeStatusTracker(statusMetric);
		}

		// grpc historgrams
		foreach (var method in Enum.GetValues<Configuration.GrpcMethod>()) {
			if (configuration.GrpcMethods.TryGetValue(method, out var label) && !string.IsNullOrWhiteSpace(label))
				trackers.GrpcTrackers[method] = new DurationTracker(grpcMethodMetric, label);
		}

		// storage writer
		if (configuration.Writer.Count > 0) {
			if (configuration.Writer.TryGetValue(Configuration.WriterTracker.FlushSize, out var flushSizeEnabled) && flushSizeEnabled) {
				var maxMetric = new MaxMetric<long>(coreMeter, "eventstore-writer-flush-size-max");
				trackers.WriterFlushSizeTracker = new MaxTracker<long>(
					metric: maxMetric,
					name: null,
					expectedScrapeIntervalSeconds: configuration.ExpectedScrapeIntervalSeconds);
			}

			if (configuration.Writer.TryGetValue(Configuration.WriterTracker.FlushDuration, out var flushDurationEnabled) && flushDurationEnabled) {
				var maxDurationmetric = new DurationMaxMetric(coreMeter, "eventstore-writer-flush-duration-max");
				trackers.WriterFlushDurationTracker = new DurationMaxTracker(
					maxDurationmetric,
					name: null,
					expectedScrapeIntervalSeconds: configuration.ExpectedScrapeIntervalSeconds);
			}
		}

		// queue trackers
		Func<string, IQueueBusyTracker> busyTrackerFactory = name => new QueueBusyTracker.NoOp();
		Func<string, IDurationMaxTracker> lengthFactory = name => new DurationMaxTracker.NoOp();
		Func<string, IQueueProcessingTracker> processingFactory = name => new QueueProcessingTracker.NoOp();

		if (configuration.Queues.TryGetValue(Configuration.QueueTracker.Busy, out var busyEnabled) && busyEnabled)
			busyTrackerFactory = name => new QueueBusyTracker(queueBusyMetric, name);

		if (configuration.Queues.TryGetValue(Configuration.QueueTracker.Length, out var lengthEnabled) && lengthEnabled)
			lengthFactory = name => new DurationMaxTracker(
				name: name,
				metric: queueQueueingDurationMaxMetric,
				expectedScrapeIntervalSeconds: configuration.ExpectedScrapeIntervalSeconds);

		if (configuration.Queues.TryGetValue(Configuration.QueueTracker.Processing, out var processingEnabled) && processingEnabled)
			processingFactory = name => new QueueProcessingTracker(queueProcessingDurationMetric, name);

		trackers.QueueTrackers = new QueueTrackers(configuration.QueueLabels, busyTrackerFactory, lengthFactory, processingFactory);

		// kestrel
		if (configuration.Kestrel.TryGetValue(Configuration.KestrelTracker.ConnectionCount, out var kestrelConnections) && kestrelConnections) {
			_ = new ConnectionMetric(coreMeter, "eventstore-kestrel-connections");
		}

		var timeout = TimeSpan.FromSeconds(1);

		// system
		var systemMetrics = new SystemMetrics(coreMeter, timeout, configuration.System);
		systemMetrics.CreateLoadAverageMetric("eventstore-sys-load-avg", new() {
			{ Configuration.SystemTracker.LoadAverage1m, "1m" },
			{ Configuration.SystemTracker.LoadAverage5m, "5m" },
			{ Configuration.SystemTracker.LoadAverage15m, "15m" },
		});

		systemMetrics.CreateCpuMetric("eventstore-sys-cpu");

		systemMetrics.CreateMemoryMetric("eventstore-sys-mem", new() {
			{ Configuration.SystemTracker.FreeMem, "free" },
			{ Configuration.SystemTracker.TotalMem, "total" },
		});

		systemMetrics.CreateDiskMetric("eventstore-sys-disk", dbConfig.Path, new() {
			{ Configuration.SystemTracker.DriveTotalBytes, "total" },
			{ Configuration.SystemTracker.DriveUsedBytes, "used" },
		});

		// process
		var processMetrics = new ProcessMetrics(coreMeter, timeout, configuration.ExpectedScrapeIntervalSeconds, configuration.Process);
		processMetrics.CreateObservableMetrics(new() {
			{ Configuration.ProcessTracker.UpTime, "eventstore-proc-up-time" },
			{ Configuration.ProcessTracker.Cpu, "eventstore-proc-cpu" },
			{ Configuration.ProcessTracker.ThreadCount, "eventstore-proc-thread-count" },
			{ Configuration.ProcessTracker.ThreadPoolPendingWorkItemCount, "eventstore-proc-thread-pool-pending-work-item-count" },
			{ Configuration.ProcessTracker.LockContentionCount, "eventstore-proc-contention-count" },
			{ Configuration.ProcessTracker.ExceptionCount, "eventstore-proc-exception-count" },
			{ Configuration.ProcessTracker.TimeInGc, "eventstore-gc-time-in-gc" },
			{ Configuration.ProcessTracker.HeapSize, "eventstore-gc-heap-size" },
			{ Configuration.ProcessTracker.HeapFragmentation, "eventstore-gc-heap-fragmentation" },
			{ Configuration.ProcessTracker.TotalAllocatedBytes, "eventstore-gc-total-allocated" },
			{ Configuration.ProcessTracker.GcPauseDuration, "eventstore-gc-pause-duration-max" },
		});

		processMetrics.CreateMemoryMetric("eventstore-proc-mem", new() {
			{ Configuration.ProcessTracker.MemWorkingSet, "working-set" },
			{ Configuration.ProcessTracker.MemPagedBytes, "paged-bytes" },
			{ Configuration.ProcessTracker.MemVirtualBytes, "virtual-bytes" },
		});

		processMetrics.CreateGcGenerationSizeMetric("eventstore-gc-generation-size", new() {
			{ Configuration.ProcessTracker.Gen0Size, "gen0" },
			{ Configuration.ProcessTracker.Gen1Size, "gen1" },
			{ Configuration.ProcessTracker.Gen2Size, "gen2" },
			{ Configuration.ProcessTracker.LohSize, "loh" },
		});

		processMetrics.CreateGcCollectionCountMetric("eventstore-gc-collection-count", new() {
			{ Configuration.ProcessTracker.Gen0CollectionCount, "gen0" },
			{ Configuration.ProcessTracker.Gen1CollectionCount, "gen1" },
			{ Configuration.ProcessTracker.Gen2CollectionCount, "gen2" },
		});

		processMetrics.CreateDiskBytesMetric("eventstore-disk-io", new() {
			{ Configuration.ProcessTracker.DiskReadBytes, "read" },
			{ Configuration.ProcessTracker.DiskWrittenBytes, "written" },
		});

		processMetrics.CreateDiskOpsMetric("eventstore-disk-io", new() {
			{ Configuration.ProcessTracker.DiskReadOps, "read" },
			{ Configuration.ProcessTracker.DiskWrittenOps, "written" },
		});
	}

	private static void LogConfig(Configuration conf) {
		var jsonSerializerSettings = new JsonSerializerSettings {
			NullValueHandling = NullValueHandling.Ignore,
		};
		jsonSerializerSettings.Converters.Add(new StringEnumConverter());

		var confJson = JsonConvert.SerializeObject(
			conf,
			Formatting.Indented,
			jsonSerializerSettings);

		Log.Information("Metrics Configuration: " + confJson);
	}
}
