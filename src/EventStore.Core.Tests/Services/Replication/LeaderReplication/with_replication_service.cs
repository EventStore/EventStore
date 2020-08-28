﻿using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Tasks;
using EventStore.Core.Authentication.InternalAuthentication;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Core.Services.Replication;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Core.Tests.Authentication;
using EventStore.Core.Tests.Authorization;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.Tests.Services.ElectionsService;
using EventStore.Core.Tests.Services.Transport.Tcp;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Data;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using EventStore.Core.Util;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.LeaderReplication {
	public abstract class with_replication_service : SpecificationWithDirectoryPerTestFixture {
		protected string EventStreamId = "test_stream";
		protected int ClusterSize = 3;
		protected InMemoryBus Publisher = new InMemoryBus("publisher");
		protected InMemoryBus TcpSendPublisher = new InMemoryBus("tcpSend");
		protected LeaderReplicationService Service;
		protected ConcurrentQueue<ReplicationTrackingMessage.ReplicaWriteAck> ReplicaWriteAcks = new ConcurrentQueue<ReplicationTrackingMessage.ReplicaWriteAck>();
		protected ConcurrentQueue<SystemMessage.VNodeConnectionLost> ReplicaLostMessages = new ConcurrentQueue<SystemMessage.VNodeConnectionLost>();
		protected ConcurrentQueue<TcpMessage.TcpSend> TcpSends = new ConcurrentQueue<TcpMessage.TcpSend>();
		private int _connectionPendingSendBytesThreshold = 10 * 1024;
		private int _connectionQueueSizeThreshold = 50000;
		protected Guid LeaderId = Guid.NewGuid();
		protected Guid ReplicaId = Guid.NewGuid();
		protected Guid ReplicaId2 = Guid.NewGuid();
		protected Guid ReadOnlyReplicaId = Guid.NewGuid();

		protected Guid ReplicaSubscriptionId;
		protected Guid ReplicaSubscriptionId2;
		protected Guid ReadOnlyReplicaSubscriptionId;

		protected TcpConnectionManager ReplicaManager1;
		protected TcpConnectionManager ReplicaManager2;
		protected TcpConnectionManager ReadOnlyReplicaManager;

		protected TFChunkDbConfig DbConfig;

		[OneTimeSetUp]
		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();
			Publisher.Subscribe(new AdHocHandler<ReplicationTrackingMessage.ReplicaWriteAck>(msg => ReplicaWriteAcks.Enqueue(msg)));
			Publisher.Subscribe(new AdHocHandler<SystemMessage.VNodeConnectionLost>(msg => ReplicaLostMessages.Enqueue(msg)));
			TcpSendPublisher.Subscribe(new AdHocHandler<TcpMessage.TcpSend>(msg => TcpSends.Enqueue(msg)));
			
			DbConfig = CreateDbConfig();
			var db = new TFChunkDb(DbConfig);
			db.Open();
			Service = new LeaderReplicationService(
				publisher: Publisher,
				instanceId: LeaderId,
				db: db,
				tcpSendPublisher: TcpSendPublisher,
				epochManager: new FakeEpochManager(),
				clusterSize: ClusterSize,
				unsafeAllowSurplusNodes: false,
				queueStatsManager: new QueueStatsManager());

			Service.Handle(new SystemMessage.SystemStart());
			Service.Handle(new SystemMessage.BecomeLeader(Guid.NewGuid()));

			ReplicaSubscriptionId = AddSubscription(ReplicaId, true, out ReplicaManager1);
			ReplicaSubscriptionId2 = AddSubscription(ReplicaId2, true, out ReplicaManager2);
			ReadOnlyReplicaSubscriptionId = AddSubscription(ReadOnlyReplicaId, false, out ReadOnlyReplicaManager);


			When();
		}

		[OneTimeTearDown]
		public override async Task TestFixtureTearDown() {
			await base.TestFixtureTearDown();
			Service.Handle(new SystemMessage.BecomeShuttingDown(Guid.NewGuid(), true, true));
		}

		private Guid AddSubscription(Guid replicaId, bool isPromotable, out TcpConnectionManager manager) {
			var tcpConn = new DummyTcpConnection() { ConnectionId = replicaId };

			manager = new TcpConnectionManager(
				"Test Subscription Connection manager", TcpServiceType.External, new ClientTcpDispatcher(Opts.WriteTimeoutMsDefault),
				InMemoryBus.CreateTest(), tcpConn, InMemoryBus.CreateTest(),
				new InternalAuthenticationProvider(InMemoryBus.CreateTest(),
					new Core.Helpers.IODispatcher(InMemoryBus.CreateTest(), new NoopEnvelope()),
					new StubPasswordHashAlgorithm(), 1, false),
				new AuthorizationGateway(new TestAuthorizationProvider()), 
				TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), (man, err) => { },
				_connectionPendingSendBytesThreshold, _connectionQueueSizeThreshold);
			var subRequest = new ReplicationMessage.ReplicaSubscriptionRequest(
				Guid.NewGuid(),
				new NoopEnvelope(),
				manager,
				0,
				Guid.NewGuid(),
				new Epoch[0],
				PortsHelper.GetLoopback(),
				LeaderId,
				replicaId,
				isPromotable);
			Service.Handle(subRequest);
			return tcpConn.ConnectionId;
		}


		public abstract void When();
		protected void BecomeLeader() {
			Service.Handle(new SystemMessage.BecomeLeader(Guid.NewGuid()));
		}

		protected void BecomeUnknown() {
			Service.Handle(new SystemMessage.BecomeUnknown(Guid.NewGuid()));
		}

		private TFChunkDbConfig CreateDbConfig() {
			ICheckpoint writerChk = new InMemoryCheckpoint(Checkpoint.Writer);
			ICheckpoint chaserChk = new InMemoryCheckpoint(Checkpoint.Chaser);
			ICheckpoint epochChk = new InMemoryCheckpoint(Checkpoint.Epoch, initValue: -1);
			ICheckpoint truncateChk = new InMemoryCheckpoint(Checkpoint.Truncate, initValue: -1);
			ICheckpoint replicationCheckpoint = new InMemoryCheckpoint(-1);
			ICheckpoint indexCheckpoint = new InMemoryCheckpoint(-1);
			var nodeConfig = new TFChunkDbConfig(
				PathName, new VersionedPatternFileNamingStrategy(PathName, "chunk-"), 1000, 10000, writerChk,
				chaserChk, epochChk, truncateChk, replicationCheckpoint, indexCheckpoint, Constants.TFChunkInitialReaderCountDefault, Constants.TFChunkMaxReaderCountDefault, true);
			return nodeConfig;
		}
	}
}
