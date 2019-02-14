using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using EventStore.Core.Bus;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;
using EventStore.Core.Tests.Integration;
using EventStore.Core.Messages;
using EventStore.Core.Data;

namespace EventStore.Core.Tests.Replication.ReadStream {
	[TestFixture, Category("LongRunning")]
	public class when_subscribed_to_stream_on_master_and_event_is_replicated_to_slaves : specification_with_cluster {
		private const string _streamId = "test-stream";
		private CountdownEvent _expectedNumberOfRoleAssignments;
		private CountdownEvent _subscriptionsConfirmed;
		private TestSubscription _masterSubscription;
		private List<TestSubscription> _slaveSubscriptions;

		private TimeSpan _timeout = TimeSpan.FromSeconds(5);

		protected override void BeforeNodesStart() {
			_nodes.ToList().ForEach(x =>
				x.Node.MainBus.Subscribe(new AdHocHandler<SystemMessage.StateChangeMessage>(Handle)));
			_expectedNumberOfRoleAssignments = new CountdownEvent(3);
			base.BeforeNodesStart();
		}

		private void Handle(SystemMessage.StateChangeMessage msg) {
			switch (msg.State) {
				case Data.VNodeState.Master:
					_expectedNumberOfRoleAssignments.Signal();
					break;
				case Data.VNodeState.Slave:
					_expectedNumberOfRoleAssignments.Signal();
					break;
			}
		}

		protected override void Given() {
			_expectedNumberOfRoleAssignments.Wait(5000);

			var master = GetMaster();
			Assert.IsNotNull(master, "Could not get master node");

			// Set the checkpoint so the check is not skipped
			master.Db.Config.ReplicationCheckpoint.Write(0);

			_subscriptionsConfirmed = new CountdownEvent(3);
			_masterSubscription = new TestSubscription(master, 1, _streamId, _subscriptionsConfirmed);
			_masterSubscription.CreateSubscription();

			_slaveSubscriptions = new List<TestSubscription>();
			var slaves = GetSlaves();
			foreach (var s in slaves) {
				var slaveSubscription = new TestSubscription(s, 1, _streamId, _subscriptionsConfirmed);
				_slaveSubscriptions.Add(slaveSubscription);
				slaveSubscription.CreateSubscription();
			}

			if (!_subscriptionsConfirmed.Wait(_timeout)) {
				Assert.Fail("Timed out waiting for subscriptions to confirm");
			}

			var events = new Event[] {new Event(Guid.NewGuid(), "test-type", false, new byte[10], new byte[0])};
			var writeResult = ReplicationTestHelper.WriteEvent(master, events, _streamId);
			Assert.AreEqual(OperationResult.Success, writeResult.Result);

			base.Given();
		}

		[Test]
		public void should_receive_event_on_master() {
			Assert.IsTrue(_masterSubscription.EventAppeared.Wait(2000));
		}

		[Test]
		public void should_receive_event_on_slaves() {
			if (!(_slaveSubscriptions[0].EventAppeared.Wait(2000) && _slaveSubscriptions[1].EventAppeared.Wait(2000))) {
				Assert.Fail("Timed out waiting for slave subscriptions to get events");
			} else {
				Assert.Pass();
			}
		}
	}
}
