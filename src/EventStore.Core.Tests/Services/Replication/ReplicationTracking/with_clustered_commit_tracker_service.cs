using System;
using System.Collections.Generic;
using System.Net;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Replication;
using EventStore.Core.TransactionLog.Checkpoint;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.ReplicationTracking {
	public abstract class with_clustered_commit_tracker_service {
		protected const int TimeoutSeconds = 5;
		protected string EventStreamId = "test_stream";
		protected int ClusterSize = 3;
		protected InMemoryBus Publisher = new InMemoryBus("publisher");
		protected ReplicationTrackingService Service;
		protected List<ReplicationTrackingMessage.ReplicatedTo> ReplicatedTos = new List<ReplicationTrackingMessage.ReplicatedTo>();
		protected ICheckpoint ReplicationCheckpoint = new InMemoryCheckpoint();
		protected ICheckpoint WriterCheckpoint = new InMemoryCheckpoint();


		[OneTimeSetUp]
		public virtual void TestFixtureSetUp() {
			Publisher.Subscribe(new AdHocHandler<ReplicationTrackingMessage.ReplicatedTo>(msg =>  ReplicatedTos.Add(msg)));
			
			Service = new ReplicationTrackingService(Publisher, ClusterSize,ReplicationCheckpoint, WriterCheckpoint);
			Service.Start();
			When();
		}

		[OneTimeTearDown]
		public virtual void TestFixtureTearDown() {
			Service.Stop();
		}

		public abstract void When();


		protected void WaitForPublish(int publishCount) {

		}
		

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
	}
}
