using System;
using System.Linq;
using System.Threading;
using EventStore.Core.Bus;
using NUnit.Framework;
using EventStore.Core.Tests.Integration;
using EventStore.Core.Messages;
using EventStore.Core.Data;

namespace EventStore.Core.Tests.Replication.ReadStream {
	[TestFixture]
	[Category("LongRunning")]
	public class when_reading_events_from_cluster_with_replication_checkpoint_not_set : specification_with_cluster {
		private CountdownEvent _expectedNumberOfRoleAssignments;

		private string _streamId = "when_reading_events_from_cluster_with_replication_checkpoint_not_set-" +
		                           Guid.NewGuid().ToString();

		private long _commitPosition;

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

			var events = new Event[] {new Event(Guid.NewGuid(), "test-type", false, new byte[10], new byte[0])};
			var writeResult = ReplicationTestHelper.WriteEvent(master, events, _streamId);
			Assert.AreEqual(OperationResult.Success, writeResult.Result);
			_commitPosition = writeResult.CommitPosition;

			// Set checkpoint to starting value
			master.Db.Config.ReplicationCheckpoint.Write(-1);
			base.Given();
		}

		[Test]
		public void should_be_able_to_read_event_from_all_forward_on_master() {
			var readResult = ReplicationTestHelper.ReadAllEventsForward(GetMaster(), _commitPosition);
			Assert.AreEqual(1, readResult.Events.Where(x => x.OriginalStreamId == _streamId).Count());
		}

		[Test]
		public void should_be_able_to_read_event_from_all_backward_on_master() {
			var readResult = ReplicationTestHelper.ReadAllEventsBackward(GetMaster(), _commitPosition);
			Assert.AreEqual(1, readResult.Events.Where(x => x.OriginalStreamId == _streamId).Count());
		}

		[Test]
		public void should_be_able_to_read_event_from_stream_forward_on_master() {
			var readResult = ReplicationTestHelper.ReadStreamEventsForward(GetMaster(), _streamId);
			Assert.AreEqual(1, readResult.Events.Count());
			Assert.AreEqual(ReadStreamResult.Success, readResult.Result);
		}

		[Test]
		public void should_be_able_to_read_event_from_stream_backward_on_master() {
			var readResult = ReplicationTestHelper.ReadStreamEventsBackward(GetMaster(), _streamId);
			Assert.AreEqual(ReadStreamResult.Success, readResult.Result);
			Assert.AreEqual(1, readResult.Events.Count());
		}

		[Test]
		public void should_be_able_to_read_event_on_master() {
			var readResult = ReplicationTestHelper.ReadEvent(GetMaster(), _streamId, 0);
			Assert.AreEqual(ReadEventResult.Success, readResult.Result);
		}
	}
}
