using System;
using System.Collections.Generic;
using System.Net;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Commit;
using EventStore.Core.TransactionLog.Checkpoint;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CommitTracking {
	public abstract class with_clustered_commit_tracker_service {
		protected const int TimeoutSeconds = 5;
		protected string EventStreamId = "test_stream";
		protected int ClusterSize = 3;
		protected InMemoryBus Publisher = new InMemoryBus("publisher");
		protected CommitTrackerService Service;
		protected List<CommitMessage.ReplicatedTo> LogCommittedTos = new List<CommitMessage.ReplicatedTo>();
		protected List<CommitMessage.CommittedTo> CommittedTos = new List<CommitMessage.CommittedTo>();

		[OneTimeSetUp]
		public virtual void TestFixtureSetUp() {
			Publisher.Subscribe(new AdHocHandler<CommitMessage.ReplicatedTo>(msg =>  LogCommittedTos.Add(msg)));
			Publisher.Subscribe(new AdHocHandler<CommitMessage.CommittedTo>(CommittedTos.Add));
			
			Service = new CommitTrackerService(Publisher, CommitLevel.MasterIndexed, ClusterSize, new InMemoryCheckpoint(0), new InMemoryCheckpoint(0));
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
