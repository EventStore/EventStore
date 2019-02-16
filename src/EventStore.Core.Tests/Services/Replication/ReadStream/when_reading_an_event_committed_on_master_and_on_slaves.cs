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
	public class when_reading_an_event_committed_on_master_and_on_slaves : specification_with_cluster {
		private CountdownEvent _expectedNumberOfRoleAssignments;

		private string _streamId =
			"when_reading_an_event_committed_on_master_and_on_slaves-" + Guid.NewGuid().ToString();

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

			// Set the checkpoint so the check is not skipped
			master.Db.Config.ReplicationCheckpoint.Write(0);

			var events = new Event[] {new Event(Guid.NewGuid(), "test-type", false, new byte[10], new byte[0])};
			var writeResult = ReplicationTestHelper.WriteEvent(master, events, _streamId);
			Assert.AreEqual(OperationResult.Success, writeResult.Result);
			_commitPosition = writeResult.CommitPosition;

			Assert.IsTrue(_commitPosition <= GetMaster().Db.Config.ReplicationCheckpoint.ReadNonFlushed(),
				"Replication checkpoint should be greater than event commit position");
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

		[Test]
		public void should_be_able_to_read_event_from_all_forward_on_slaves() {
			var slaves = GetSlaves();
			var quorum = (slaves.Count() + 1) / 2 + 1;
			var successfulReads = 0;
			foreach (var s in slaves) {
				var readResult = ReplicationTestHelper.ReadAllEventsForward(s, _commitPosition);
				successfulReads += readResult.Events.Where(x => x.OriginalStreamId == _streamId).Count();
			}

			Assert.GreaterOrEqual(successfulReads, quorum - 1);
		}

		[Test]
		public void should_be_able_to_read_event_from_all_backward_on_slaves() {
			var slaves = GetSlaves();
			var quorum = (slaves.Count() + 1) / 2 + 1;
			var successfulReads = 0;
			foreach (var s in slaves) {
				var readResult = ReplicationTestHelper.ReadAllEventsBackward(s, _commitPosition);
				successfulReads += readResult.Events.Where(x => x.OriginalStreamId == _streamId).Count();
			}

			Assert.GreaterOrEqual(successfulReads, quorum - 1);
		}

		[Test]
		public void should_be_able_to_read_event_from_stream_forward_on_slaves() {
			var slaves = GetSlaves();
			var quorum = (slaves.Count() + 1) / 2 + 1;
			var successfulReads = 0;
			foreach (var s in slaves) {
				var readResult = ReplicationTestHelper.ReadStreamEventsForward(s, _streamId);
				successfulReads += readResult.Events.Count();
				Assert.AreEqual(ReadStreamResult.Success, readResult.Result);
			}

			Assert.GreaterOrEqual(successfulReads, quorum - 1);
		}

		[Test]
		public void should_be_able_to_read_event_from_stream_backward_on_slaves() {
			var slaves = GetSlaves();
			var quorum = (slaves.Count() + 1) / 2 + 1;
			var successfulReads = 0;
			foreach (var s in slaves) {
				var readResult = ReplicationTestHelper.ReadStreamEventsBackward(s, _streamId);
				successfulReads += readResult.Events.Count();
			}

			Assert.GreaterOrEqual(successfulReads, quorum - 1);
		}

		[Test]
		public void should_be_able_to_read_event_on_slaves() {
			var slaves = GetSlaves();
			var quorum = (slaves.Count() + 1) / 2 + 1;
			var successfulReads = 0;
			foreach (var s in slaves) {
				var readResult = ReplicationTestHelper.ReadEvent(s, _streamId, 0);
				successfulReads += readResult.Result == ReadEventResult.Success ? 1 : 0;
			}

			Assert.GreaterOrEqual(successfulReads, quorum - 1);
		}
	}
}
