﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Replication;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Core.Tests.Authentication;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.Tests.Services.ElectionsService;
using EventStore.Core.Tests.Services.Transport.Tcp;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.ReplicationService {
	public abstract class with_replication_service : SpecificationWithDirectoryPerTestFixture {
		protected const int TimeoutSeconds = 5;
		protected string EventStreamId = "test_stream";
		protected int ClusterSize = 3;
		protected InMemoryBus Publisher = new InMemoryBus("publisher");
		protected InMemoryBus TcpSendPublisher = new InMemoryBus("tcpSend");
		protected MasterReplicationService Service;
		protected List<CommitMessage.ReplicaWrittenTo> ReplicaLogWrittenTos = new List<CommitMessage.ReplicaWrittenTo>();
		protected List<TcpMessage.TcpSend> TcpSends = new List<TcpMessage.TcpSend>();
		private int _connectionPendingSendBytesThreshold = 10 * 1024;
		private int _connectionQueueSizeThreshold = 50000;
		protected Guid MasterId = Guid.NewGuid();
		protected Guid ReplicaId = Guid.NewGuid();
		protected Guid ReplicaId2 = Guid.NewGuid();
		protected Guid ReadOnlyReplicaId = Guid.NewGuid();

		protected Guid ReplicaSubscriptionId;
		protected Guid ReplicaSubscriptionId2;
		protected Guid ReadOnlyReplicaSubscriptionId;

		[OneTimeSetUp]
		public async override Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();
			Publisher.Subscribe(new AdHocHandler<CommitMessage.ReplicaWrittenTo>(msg => ReplicaLogWrittenTos.Add(msg)));
			TcpSendPublisher.Subscribe(new AdHocHandler<TcpMessage.TcpSend>(msg => TcpSends.Add(msg)));
			var writerCheckpoint = new InMemoryCheckpoint(0);
			var chaserCheckpoint = new InMemoryCheckpoint(0);


			var db = new TFChunkDb(CreateDbConfig());
			db.Open();
			Service = new MasterReplicationService(
				publisher: Publisher,
				instanceId: MasterId,
				db: db,
				tcpSendPublisher: TcpSendPublisher,
				epochManager: new FakeEpochManager(),
				clusterSize: ClusterSize,
				queueStatsManager: new QueueStatsManager());

			Service.Handle(new SystemMessage.SystemStart());
			Service.Handle(new SystemMessage.BecomeMaster(Guid.NewGuid()));

			ReplicaSubscriptionId = AddSubscription(ReplicaId, true);
			ReplicaSubscriptionId2 = AddSubscription(ReplicaId2, true);
			ReadOnlyReplicaSubscriptionId = AddSubscription(ReadOnlyReplicaId, false);


			When();
		}

		[OneTimeTearDown]
		public async override Task TestFixtureTearDown() {
			await base.TestFixtureTearDown();
			Service.Handle(new SystemMessage.BecomeShuttingDown(Guid.NewGuid(), true, true));
		}

		private Guid AddSubscription(Guid replicaId, bool isPromotable) {
			var tcpConn = new DummyTcpConnection() { ConnectionId = replicaId };

			var mgr = new TcpConnectionManager(
				"Test Subscription Connection mager", TcpServiceType.External, new ClientTcpDispatcher(),
				InMemoryBus.CreateTest(), tcpConn, InMemoryBus.CreateTest(),
				new InternalAuthenticationProvider(
					new Core.Helpers.IODispatcher(InMemoryBus.CreateTest(), new NoopEnvelope()),
					new StubPasswordHashAlgorithm(), 1, false),
				TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), (man, err) => { },
				_connectionPendingSendBytesThreshold, _connectionQueueSizeThreshold);
			var subRequest = new ReplicationMessage.ReplicaSubscriptionRequest(
				Guid.NewGuid(),
				new NoopEnvelope(),
				mgr,
				0,
				Guid.NewGuid(),
				new Epoch[0],
				PortsHelper.GetLoopback(),
				MasterId,
				replicaId,
				isPromotable);
			Service.Handle(subRequest);
			return tcpConn.ConnectionId;
		}


		public abstract void When();
		protected void BecomeMaster() {
			Service.Handle(new SystemMessage.BecomeMaster(Guid.NewGuid()));
		}

		protected void BecomeUnknown() {
			Service.Handle(new SystemMessage.BecomeUnknown(Guid.NewGuid()));
		}

		protected void BecomeSlave() {
			var masterIpEndPoint = new IPEndPoint(IPAddress.Loopback, 2113);
			Service.Handle(new SystemMessage.BecomeSlave(Guid.NewGuid(), new VNodeInfo(Guid.NewGuid(), 1,
				masterIpEndPoint, masterIpEndPoint, masterIpEndPoint,
				masterIpEndPoint, masterIpEndPoint, masterIpEndPoint, false)));
		}
		private TFChunkDbConfig CreateDbConfig() {
			ICheckpoint writerChk = new InMemoryCheckpoint(Checkpoint.Writer);
			ICheckpoint chaserChk = new InMemoryCheckpoint(Checkpoint.Chaser);
			ICheckpoint epochChk = new InMemoryCheckpoint(Checkpoint.Epoch, initValue: -1);
			ICheckpoint truncateChk = new InMemoryCheckpoint(Checkpoint.Truncate, initValue: -1);
			ICheckpoint replicationCheckpoint = new InMemoryCheckpoint(-1);
			var nodeConfig = new TFChunkDbConfig(
				PathName, new VersionedPatternFileNamingStrategy(PathName, "chunk-"), 1000, 10000, writerChk,
				chaserChk, epochChk, truncateChk, replicationCheckpoint, Constants.TFChunkInitialReaderCountDefault, Constants.TFChunkMaxReaderCountDefault, true);
			return nodeConfig;
		}
	}
}
