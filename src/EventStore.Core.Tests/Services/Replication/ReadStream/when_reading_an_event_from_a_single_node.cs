using System;
using System.Linq;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;
using EventStore.Core.Tests.Integration;
using EventStore.Core.Messages;
using EventStore.Core.Data;

namespace EventStore.Core.Tests.Replication.ReadStream {
	[TestFixture]
	[Category("LongRunning")]
	public class when_reading_an_event_from_a_single_node : specification_with_cluster {
		private CountdownEvent _expectedNumberOfRoleAssignments;
		private string _streamId = "test-stream";
		private long _commitPosition;

		private MiniClusterNode _liveNode;

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

			_liveNode = GetMaster();
			Assert.IsNotNull(_liveNode, "Could not get master node");

			var events = new Event[] {new Event(Guid.NewGuid(), "test-type", false, new byte[10], new byte[0])};
			var writeResult = ReplicationTestHelper.WriteEvent(_liveNode, events, _streamId);
			Assert.AreEqual(OperationResult.Success, writeResult.Result);
			_commitPosition = writeResult.CommitPosition;

			var slaves = GetSlaves();
			foreach (var s in slaves) {
				ShutdownNode(s.DebugIndex);
			}

			base.Given();
		}

		[Test]
		public void should_be_able_to_read_event_from_all_forward() {
			var readResult = ReplicationTestHelper.ReadAllEventsForward(_liveNode, _commitPosition);
			Assert.AreEqual(1, readResult.Events.Where(x => x.OriginalStreamId == _streamId).Count());
		}

		[Test]
		public void should_be_able_to_read_event_from_all_backward() {
			var readResult = ReplicationTestHelper.ReadAllEventsBackward(_liveNode, _commitPosition);
			Assert.AreEqual(1, readResult.Events.Where(x => x.OriginalStreamId == _streamId).Count());
		}

		[Test]
		public void should_not_be_able_to_read_event_from_stream_forward() {
			var readResult = ReplicationTestHelper.ReadStreamEventsForward(_liveNode, _streamId);
			Assert.AreEqual(1, readResult.Events.Count());
			Assert.AreEqual(ReadStreamResult.Success, readResult.Result);
		}

		[Test]
		public void should_not_be_able_to_read_event_from_stream_backward() {
			var readResult = ReplicationTestHelper.ReadStreamEventsBackward(_liveNode, _streamId);
			Assert.AreEqual(1, readResult.Events.Count());
			Assert.AreEqual(ReadStreamResult.Success, readResult.Result);
		}

		[Test]
		public void should_not_be_able_to_read_event() {
			var readResult = ReplicationTestHelper.ReadEvent(_liveNode, _streamId, 0);
			Assert.AreEqual(ReadEventResult.Success, readResult.Result);
		}
	}
}
