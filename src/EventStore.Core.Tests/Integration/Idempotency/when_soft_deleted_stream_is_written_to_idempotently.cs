using System;
using System.Threading.Tasks;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.UserManagement;
using EventStore.Core.TransactionLog.Data;
using NUnit.Framework;

namespace EventStore.Core.Tests.Integration.Idempotency {
	public class when_soft_deleted_stream_is_written_to_idempotently : specification_with_a_single_node {
		private readonly string _streamId;
		private readonly Event[] _events;

		public when_soft_deleted_stream_is_written_to_idempotently() {
			_streamId = $"{nameof(when_soft_deleted_stream_is_written_to_idempotently)}-{Guid.NewGuid()}";
			_events = new[] {new Event(Guid.NewGuid(), "event-type", false, new byte[] { }, new byte[] { })};
		}

		protected override async Task Given() {
			var writeEventsCompleted = new TaskCompletionSource<bool>();
			_node.Node.MainQueue.Publish(new ClientMessage.WriteEvents(Guid.NewGuid(), Guid.NewGuid(),
				new CallbackEnvelope(
					_ => {
						writeEventsCompleted.SetResult(true);
					}), false, _streamId, ExpectedVersion.NoStream, _events, SystemAccounts.System));

			await writeEventsCompleted.Task
				.WithTimeout(TimeSpan.FromSeconds(2));

			var deleteStreamCompleted = new TaskCompletionSource<bool>();
			_node.Node.MainQueue.Publish(new ClientMessage.DeleteStream(Guid.NewGuid(), Guid.NewGuid(),
				new CallbackEnvelope(
					_ => {
						deleteStreamCompleted.SetResult(true);
					}), false, _streamId, ExpectedVersion.Any, false, SystemAccounts.System));

			await deleteStreamCompleted.Task
				.WithTimeout(TimeSpan.FromSeconds(2));
		}

		[Test]
		public async Task should_return_negative_1_as_log_position() {
			var writeEventsCompleted = new TaskCompletionSource<ClientMessage.WriteEventsCompleted>();
			
			_node.Node.MainQueue.Publish(new ClientMessage.WriteEvents(Guid.NewGuid(), Guid.NewGuid(),
				new CallbackEnvelope(
					msg => {
						writeEventsCompleted.SetResult(msg as ClientMessage.WriteEventsCompleted);
					}), false, _streamId, ExpectedVersion.NoStream, _events, SystemAccounts.System));
			
			var completed = await writeEventsCompleted.Task
				.WithTimeout(TimeSpan.FromSeconds(2));
			
			Assert.AreEqual(-1, completed.CommitPosition);
			Assert.AreEqual(-1, completed.PreparePosition);
		}
	}
}
