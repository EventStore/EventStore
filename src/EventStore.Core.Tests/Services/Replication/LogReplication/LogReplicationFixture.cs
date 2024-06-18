using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Authentication.PassthroughAuthentication;
using EventStore.Core.Authorization;
using EventStore.Core.Bus;
using EventStore.Core.Cluster;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Metrics;
using EventStore.Core.Services;
using EventStore.Core.Services.Replication;
using EventStore.Core.Services.Storage;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.Tests.Services.ElectionsService;
using EventStore.Core.Tests.Services.Storage;
using EventStore.Core.Tests.Services.Storage.ReadIndex;
using EventStore.Core.Time;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using Serilog;
using Serilog.Events;

namespace EventStore.Core.Tests.Services.Replication.LogReplication;

public abstract class LogReplicationFixture<TLogFormat, TStreamId> : SpecificationWithDirectoryPerTestFixture {
	private const int ClusterSize = 3;
	private readonly EndPoint FakeEndPoint = new IPEndPoint(IPAddress.Loopback, 5555);

	private readonly Disposables _disposables = new();
	private LeaderInfo<TStreamId> _leaderInfo;
	private ReplicaInfo<TStreamId> _replicaInfo;

	protected const int ChunkSize = 1 * 1024 * 1024;
	protected long LeaderWriterCheckpoint => _leaderInfo.Db.Config.WriterCheckpoint.ReadNonFlushed();
	protected long ReplicaWriterCheckpoint => _replicaInfo.Db.Config.WriterCheckpoint.ReadNonFlushed();

	private TFChunkDb CreateDb(string path) {
		var dbPath = Path.Combine(PathName, path);
		Directory.CreateDirectory(dbPath);

		var dbConfig = new TFChunkDbConfig(
			path: dbPath,
			fileNamingStrategy: new VersionedPatternFileNamingStrategy(dbPath, "chunk-"),
			chunkSize: ChunkSize,
			maxChunksCacheSize: 2 * ChunkSize,
			writerCheckpoint: new InterceptorCheckpoint(new InMemoryCheckpoint()),
			chaserCheckpoint: new InMemoryCheckpoint(),
			epochCheckpoint: new InMemoryCheckpoint(),
			proposalCheckpoint: new InMemoryCheckpoint(),
			truncateCheckpoint: new InMemoryCheckpoint(),
			replicationCheckpoint: new InMemoryCheckpoint(-1),
			indexCheckpoint: new InMemoryCheckpoint(-1),
			streamExistenceFilterCheckpoint: new InMemoryCheckpoint());

		return new TFChunkDb(config: dbConfig);
	}

	private class ZeroDurationTracker : IDurationMaxTracker {
		public Instant RecordNow(Instant start) => start;
	}

	private StorageWriterService<TStreamId> CreateStorageWriter(TFChunkDb db, ISubscriber inputBus, IPublisher outputBus) {
		var writer = new TFChunkWriter(db);
		var logFormat = LogFormatHelper<TLogFormat, TStreamId>.LogFormatFactory.Create(new() {
			IndexDirectory = Path.Combine(db.Config.Path, "index")
		});
		logFormat.StreamNamesProvider.SetReader(new FakeIndexReader<TStreamId>());
		logFormat.StreamNamesProvider.SetTableIndex(new FakeInMemoryTableIndex<TStreamId>());

		var trackers = new Trackers();

		var storageWriterService = new ClusterStorageWriterService<TStreamId>(
			bus: outputBus,
			subscribeToBus: inputBus,
			minFlushDelay: TimeSpan.Zero, // to force a flush on each write to easily detect when a write is complete
			db: db,
			writer: writer,
			indexWriter: new FakeIndexWriter<TStreamId>(),
			recordFactory: logFormat.RecordFactory,
			streamNameIndex: logFormat.StreamNameIndex,
			eventTypeIndex: logFormat.EventTypeIndex,
			emptyEventTypeId: logFormat.EmptyEventTypeId,
			systemStreams: logFormat.SystemStreams,
			epochManager: new FakeEpochManager(),
			queueStatsManager: new QueueStatsManager(),
			trackers: trackers.QueueTrackers,
			flushSizeTracker: trackers.WriterFlushSizeTracker,
			flushDurationTracker: new ZeroDurationTracker(), // to force a flush on each write to easily detect when a write is complete
			getLastIndexedPosition: () => -1);

		storageWriterService.Start();

		return storageWriterService;
	}

	private LeaderInfo<TStreamId> CreateLeader(TFChunkDb db) {
		db.Open(createNewChunks: true);

		// we don't need a controller here, so we use the same bus for subscribing and publishing
		var subscribeBus = new InMemoryBus("subscribeBus");
		var publishBus = subscribeBus;

		var writer = CreateStorageWriter(
			db: db,
			inputBus: subscribeBus,
			outputBus: publishBus);

		var port = PortsHelper.GetAvailablePort(IPAddress.Loopback);
		var networkSendBus = new InMemoryBus("networkSendBus");
		var dispatcher = new InternalTcpDispatcher(writeTimeout: TimeSpan.FromSeconds(5));

		var tcpService = new TcpService(
			publisher: publishBus,
			serverEndPoint: new IPEndPoint(IPAddress.Loopback, port),
			networkSendQueue: networkSendBus,
			serviceType: TcpServiceType.Internal,
			dispatcher: dispatcher,
			securityType: TcpSecurityType.Normal,
			heartbeatInterval: TimeSpan.FromSeconds(1),
			heartbeatTimeout: TimeSpan.FromSeconds(5),
			authProvider: new PassthroughAuthenticationProvider(),
			authorizationGateway: new AuthorizationGateway(new PassthroughAuthorizationProvider()),
			certificateSelector: null,
			intermediatesSelector: null,
			sslClientCertValidator: null,
			connectionPendingSendBytesThreshold: 1_000_000,
			connectionQueueSizeThreshold: 1_000);

		subscribeBus.Subscribe<SystemMessage.SystemInit>(tcpService);
		subscribeBus.Subscribe<SystemMessage.SystemStart>(tcpService);
		subscribeBus.Subscribe<SystemMessage.BecomeShuttingDown>(tcpService);

		var leaderInstanceId = Guid.NewGuid();
		var epochManager = new FakeEpochManager();

		var leaderReplicationService = new LeaderReplicationService(
			publisher: publishBus,
			instanceId: leaderInstanceId,
			db: db,
			tcpSendPublisher: networkSendBus,
			epochManager: epochManager,
			clusterSize: ClusterSize,
			unsafeAllowSurplusNodes: false,
			queueStatsManager: new QueueStatsManager());

		var tcpSendService = new TcpSendService();
		networkSendBus.Subscribe(tcpSendService);

		subscribeBus.Subscribe<SystemMessage.SystemStart>(leaderReplicationService);
		subscribeBus.Subscribe<SystemMessage.StateChangeMessage>(leaderReplicationService);
		subscribeBus.Subscribe<SystemMessage.EnablePreLeaderReplication>(leaderReplicationService);
		subscribeBus.Subscribe<ReplicationMessage.ReplicaSubscriptionRequest>(leaderReplicationService);
		subscribeBus.Subscribe<ReplicationMessage.ReplicaLogPositionAck>(leaderReplicationService);
		subscribeBus.Subscribe<ReplicationTrackingMessage.ReplicatedTo>(leaderReplicationService);

		return new LeaderInfo<TStreamId>() {
			Db = db,
			Publisher = publishBus,
			ReplicationService = leaderReplicationService,
			EpochManager = epochManager,
			MemberInfo = MemberInfo.ForVNode(
				instanceId: leaderInstanceId,
				timeStamp: DateTime.Now,
				state: VNodeState.Leader,
				isAlive: true,
				internalTcpEndPoint: new IPEndPoint(IPAddress.Loopback, port),
				internalSecureTcpEndPoint: null,
				externalTcpEndPoint: null,
				externalSecureTcpEndPoint: null,
				httpEndPoint: FakeEndPoint,
				advertiseHostToClientAs: null,
				advertiseHttpPortToClientAs: 0,
				advertiseTcpPortToClientAs: 0,
				lastCommitPosition: 0,
				writerCheckpoint: 0,
				chaserCheckpoint: 0,
				epochPosition: 0,
				epochNumber: 0,
				epochId: Guid.NewGuid(),
				nodePriority: 0,
				isReadOnlyReplica: false),
			Writer = writer
		};
	}

	private ReplicaInfo<TStreamId> CreateReplica(TFChunkDb db, LeaderInfo<TStreamId> leaderInfo) {
		db.Open(createNewChunks: false);

		var subscribeBus = new InMemoryBus("subscribeBus");
		var adhocReplicaController = new AdHocReplicaController<TStreamId>(subscribeBus, leaderInfo);
		var publishBus = adhocReplicaController.Publisher;

		var replicationInterceptor = new ReplicationInterceptor(subscribeBus);
		var writer = CreateStorageWriter(
			db: db,
			inputBus: replicationInterceptor.Bus,
			outputBus: publishBus);

		var epochManager = new FakeEpochManager();
		var networkSendBus = new InMemoryBus("networkSendBus");

		var replicaService = new ReplicaService(
			publisher: publishBus,
			db: db,
			epochManager: epochManager,
			networkSendQueue: networkSendBus,
			authProvider: new PassthroughAuthenticationProvider(),
			authorizationGateway: new AuthorizationGateway(new PassthroughAuthorizationProvider()),
			internalTcp: FakeEndPoint,
			isReadOnlyReplica: false,
			useSsl: false,
			sslServerCertValidator: null,
			sslClientCertificateSelector: null,
			heartbeatTimeout: TimeSpan.FromSeconds(5),
			heartbeatInterval: TimeSpan.FromSeconds(1),
			writeTimeout: TimeSpan.FromSeconds(5)
		);

		var tcpSendService = new TcpSendService();
		networkSendBus.Subscribe(tcpSendService);

		subscribeBus.Subscribe<SystemMessage.StateChangeMessage>(replicaService);
		subscribeBus.Subscribe<ReplicationMessage.ReconnectToLeader>(replicaService);
		subscribeBus.Subscribe<ReplicationMessage.SubscribeToLeader>(replicaService);
		subscribeBus.Subscribe<ReplicationMessage.AckLogPosition>(replicaService);
		subscribeBus.Subscribe<ClientMessage.TcpForwardMessage>(replicaService);

		return new ReplicaInfo<TStreamId> {
			Db = db,
			Publisher = publishBus,
			ReplicaService = replicaService,
			EpochManager = epochManager,
			Writer = writer,
			GetNumWriterFlushes = () => adhocReplicaController.NumWriterFlushes,
			ReplicationInterceptor = replicationInterceptor,
			ConnectionEstablished = adhocReplicaController.ConnectionToLeaderEstablished,
			GetReplicationPosition = () => adhocReplicaController.LastAck.WriterPosition,
			ResetSubscription = () => adhocReplicaController.ResetSubscription()
		};
	}

	private void StartLeader() {
		_leaderInfo.Publisher.Publish(new SystemMessage.SystemInit());
		_leaderInfo.Publisher.Publish(new SystemMessage.SystemStart());
		_leaderInfo.Publisher.Publish(new SystemMessage.BecomeLeader(Guid.NewGuid()));
	}

	private void StartReplica(bool pauseReplication = true) {
		_replicaInfo.ResetSubscription();
		_replicaInfo.ReplicationInterceptor.Reset(pauseReplication: pauseReplication);
		_replicaInfo.Publisher.Publish(new SystemMessage.BecomePreReplica(
			correlationId: Guid.NewGuid(),
			leaderConnectionCorrelationId: Guid.NewGuid(),
			leader: _leaderInfo.MemberInfo));
		_replicaInfo.ConnectionEstablished.WaitOne();
		_replicaInfo.Publisher.Publish(new ReplicationMessage.SubscribeToLeader(
			stateCorrelationId: Guid.NewGuid(),
			leaderId: _leaderInfo.MemberInfo.InstanceId,
			subscriptionId: Guid.NewGuid()));
	}

	private void SetUpLogging() {
		Log.Logger = new LoggerConfiguration()
			//.WriteTo.Console(standardErrorFromLevel: LogEventLevel.Verbose) // uncomment to enable console logging
			.MinimumLevel.Verbose()
			.CreateLogger();
	}

	[OneTimeSetUp]
	public override Task TestFixtureSetUp() {
		base.TestFixtureSetUp();
		SetUpLogging();

		return Task.CompletedTask;
	}

	[SetUp]
	public virtual async Task SetUp() {
		var runId = Guid.NewGuid();

		var leaderDb = CreateDb($"leader-{runId}").DisposeWith(_disposables);
		var replicaDb = CreateDb($"replica-{runId}").DisposeWith(_disposables);

		await SetUpDbs(leaderDb, replicaDb);

		_leaderInfo = CreateLeader(leaderDb);
		_replicaInfo = CreateReplica(replicaDb, _leaderInfo);

		AddEpoch(epochNumber: 0, epochPosition: 0);
		StartLeader();
	}

	protected virtual Task SetUpDbs(TFChunkDb leaderDb, TFChunkDb replicaDb) => Task.CompletedTask;

	private void AddEpoch(int epochNumber, long epochPosition) {
		var epoch = new EpochRecord(
			epochPosition: epochPosition,
			epochNumber: epochNumber,
			epochId: Guid.NewGuid(),
			prevEpochPosition: -1,
			timeStamp: DateTime.Now,
			leaderInstanceId: Guid.Empty);

		_leaderInfo.EpochManager.CacheEpoch(epoch);
		_replicaInfo.EpochManager.CacheEpoch(epoch);
	}

	private Event[] CreateEvents(string streamId, string[] eventDatas) {
		var events = new Event[eventDatas.Length];
		for(var i = 0; i < events.Length; i++)
			events[i] = new Event(Guid.NewGuid(), "type", false, eventDatas[i], null);

		return events;
	}

	protected Task<long> WriteEvents(string streamId, params string[] eventDatas) {
		var events = CreateEvents(streamId, eventDatas);
		return WriteEvents(
			streamId: streamId,
			events: events,
			publisher: _leaderInfo.Publisher,
			writerChk: (InterceptorCheckpoint) _leaderInfo.Db.Config.WriterCheckpoint,
			chunkSize: _leaderInfo.Db.Config.ChunkSize);
	}

	protected Task<long> WriteEventsToReplica(string streamId, params string[] eventDatas) {
		var events = CreateEvents(streamId, eventDatas);
		return WriteEvents(
			streamId: streamId,
			events: events,
			publisher: _replicaInfo.Publisher,
			writerChk: (InterceptorCheckpoint) _replicaInfo.Db.Config.WriterCheckpoint,
			chunkSize: _replicaInfo.Db.Config.ChunkSize);
	}

	private Task<long> WriteEvents (
		string streamId,
		Event[] events,
		IPublisher publisher,
		InterceptorCheckpoint writerChk,
		int chunkSize) {
		// we keep track of write completion by verifying if the list of writer checkpoints has changed.
		// we cannot simply wait for a single checkpoint change as some writes may move the writer checkpoint multiple times. 
		// since chunk completion also moves the writer checkpoint, we need to keep track of it.
		// finally, we wait for the writer checkpoint to be flushed (this is guaranteed as we force a flush when setting up ClusterStorageWriterService) 

		var flushedWriterPos = writerChk.Read();
		var writerChks = writerChk.Values.ToArray();

		if (flushedWriterPos > 0) {
			Assert.Greater(writerChks.Length, 0, "The writer checkpoint has been flushed but the list of writer checkpoints is empty");
			Assert.AreEqual(flushedWriterPos, writerChks[^1], "There are pending writer checkpoint flushes");
		}

		publisher.Publish(new StorageMessage.WritePrepares(
			correlationId: Guid.NewGuid(),
			envelope: new FakeEnvelope(),
			eventStreamId: streamId,
			expectedVersion: -1,
			events: events,
			cancellationToken: CancellationToken.None));

		long[] newWriterChks = null;
		AssertEx.IsOrBecomesTrue(
			func: () => {
				newWriterChks = writerChk.Values.ToArray();

				if (newWriterChks.Length == writerChks.Length + 1) {
					if (newWriterChks[^1] % chunkSize == 0)
						return false; // chunk has been completed

					// write completed (not at end of chunk)
					return true;
				}

				if (newWriterChks.Length == writerChks.Length + 2) // write completed after completing chunk
					return true;

				return false; // no write completed yet
			}, TimeSpan.FromSeconds(5));

		// wait for the writer checkpoint to be flushed
		AssertEx.IsOrBecomesTrue(
			func: () => newWriterChks[^1] == writerChk.Read(),
			TimeSpan.FromSeconds(5));

		flushedWriterPos = newWriterChks[^1];
		return Task.FromResult(flushedWriterPos);
	}

	protected Task ConnectReplica(bool pauseReplication = false) {
		StartReplica(pauseReplication: pauseReplication);
		return Task.CompletedTask;
	}

	protected async Task ReconnectReplica(bool pauseReplication = false) {
		await ConnectReplica(pauseReplication: pauseReplication);
	}

	protected Task ReplicaBecomesLeader() {
		_replicaInfo.ResetSubscription();
		_replicaInfo.ReplicationInterceptor.Reset(pauseReplication: false);
		_replicaInfo.Publisher.Publish(new SystemMessage.BecomePreLeader(
			correlationId: Guid.NewGuid()));
		_replicaInfo.Publisher.Publish(new SystemMessage.BecomeLeader(
			correlationId: Guid.NewGuid()));

		return Task.CompletedTask;
	}

	protected async Task ResumeReplicationUntil(long maxLogPosition, int expectedFlushes) {
		var initialFlushes = _replicaInfo.GetNumWriterFlushes();
		_replicaInfo.ReplicationInterceptor.ResumeUntil(maxLogPosition);
		await ReplicationPaused();
		await ReplicaWriterFlushed(initialFlushes, expectedFlushes);
	}

	protected async Task ResumeReplicationUntil(int rawChunkStartNumber, int rawChunkEndNumber, int maxRawPosition, int expectedFlushes) {
		var initialFlushes = _replicaInfo.GetNumWriterFlushes();
		_replicaInfo.ReplicationInterceptor.ResumeUntil(rawChunkStartNumber, rawChunkEndNumber, maxRawPosition);
		await ReplicationPaused();
		await ReplicaWriterFlushed(initialFlushes, expectedFlushes);
	}

	private Task ReplicationPaused() {
		AssertEx.IsOrBecomesTrue(
			func: () => _replicaInfo.ReplicationInterceptor.Paused,
			timeout: TimeSpan.FromSeconds(5));
		return Task.CompletedTask;
	}

	private Task ReplicaWriterFlushed(int initialFlushes, int expectedFlushes) {
		AssertEx.IsOrBecomesTrue(
			func: () => _replicaInfo.GetNumWriterFlushes() >= initialFlushes + expectedFlushes,
			timeout: TimeSpan.FromSeconds(5));
		return Task.CompletedTask;
	}

	protected Task Replicated(TimeSpan? timeout = null) {
		var writerPos = _leaderInfo.Db.Config.WriterCheckpoint.ReadNonFlushed();

		AssertEx.IsOrBecomesTrue(
			func: () => _replicaInfo.GetReplicationPosition() >= writerPos,
			timeout: timeout ?? TimeSpan.FromSeconds(5));

		return Task.CompletedTask;
	}

	protected void VerifyCheckpoints(int expectedCheckpoints) =>
		VerifyCheckpoints(
			expectedLeaderCheckpoints: expectedCheckpoints,
			expectedReplicaCheckpoints: expectedCheckpoints);

	protected void VerifyCheckpoints(int expectedLeaderCheckpoints, int expectedReplicaCheckpoints) {
		var leaderCheckpoints = ((InterceptorCheckpoint) _leaderInfo.Db.Config.WriterCheckpoint).Values.ToArray();
		var replicaCheckpoints = ((InterceptorCheckpoint) _replicaInfo.Db.Config.WriterCheckpoint).Values.ToArray();

		Assert.AreEqual(expectedLeaderCheckpoints, leaderCheckpoints.Length);
		Assert.AreEqual(expectedReplicaCheckpoints, replicaCheckpoints.Length);
		Assert.GreaterOrEqual(leaderCheckpoints.Length, replicaCheckpoints.Length);
		Assert.AreEqual(leaderCheckpoints[..replicaCheckpoints.Length], replicaCheckpoints);
	}

	protected void VerifyDB(int expectedLogicalChunks) {
		var numChunksOnLeader = _leaderInfo.Db.Manager.ChunksCount;
		var numChunksOnReplica = _replicaInfo.Db.Manager.ChunksCount;
		var atChunkBoundary = _leaderInfo.Db.Config.WriterCheckpoint.Read() % _leaderInfo.Db.Config.ChunkSize == 0;

		Assert.AreEqual(expectedLogicalChunks, numChunksOnLeader);

		if (atChunkBoundary)
			expectedLogicalChunks--;

		Assert.AreEqual(expectedLogicalChunks, numChunksOnReplica);

		for (var chunkNum = 0; chunkNum < expectedLogicalChunks;) {
			var leaderChunk = _leaderInfo.Db.Manager.GetChunk(chunkNum);
			var replicaChunk = _replicaInfo.Db.Manager.GetChunk(chunkNum);

			VerifyHeader(leaderChunk.ChunkHeader, replicaChunk.ChunkHeader);
			var leaderData = ReadChunkData(leaderChunk.FileName, excludeChecksum: leaderChunk.ChunkFooter != null);
			var replicaData = ReadChunkData(replicaChunk.FileName, excludeChecksum: replicaChunk.ChunkFooter != null);
			Assert.True(leaderData.SequenceEqual(replicaData));

			chunkNum = leaderChunk.ChunkHeader.ChunkEndNumber + 1;
		}

		if (atChunkBoundary) {
			// verify that the chunk data is empty on the leader
			var leaderChunk = _leaderInfo.Db.Manager.GetChunk(expectedLogicalChunks);
			var leaderData = ReadChunkData(leaderChunk.FileName, excludeChecksum: false);
			Assert.True(leaderData.SequenceEqual(new byte[leaderData.Length]));
		}
	}

	private void VerifyHeader(ChunkHeader header1, ChunkHeader header2) {
		Assert.True(header1.AsByteArray().SequenceEqual(header2.AsByteArray()));
	}

	private static ReadOnlySpan<byte> ReadChunkData(string fileName, bool excludeChecksum) {
		using var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan);
		var fi = new FileInfo(fileName);
		var data = new byte[fi.Length].AsSpan();

		int pos = 0; int read;
		while ((read = fs.Read(data[pos..])) > 0)
			pos += read;

		return excludeChecksum ? data[TFConsts.ChunkHeaderSize..^ChunkFooter.ChecksumSize] : data[TFConsts.ChunkHeaderSize..];
	}

	[TearDown]
	public virtual Task TearDown() {
		var shutdownMsg = new SystemMessage.BecomeShuttingDown(
			correlationId: Guid.NewGuid(),
			exitProcess: false,
			shutdownHttp: true);

		_leaderInfo.Publisher.Publish(shutdownMsg);
		_replicaInfo.Publisher.Publish(shutdownMsg);

		_disposables?.Dispose();

		return Task.CompletedTask;
	}
}
