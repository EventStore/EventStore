using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Client = EventStore.ClientAPI;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.Tests.Integration;
using EventStore.Core.Tests.Services.Replication;
using NUnit.Framework;
using EventStore.ClientAPI.Common.Utils;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using System.Threading.Tasks;
using EventStore.Core.Bus;

namespace EventStore.Core.Tests.Services.RequestManagement.Integration {
	public class when_commiting_a_transaction_with_no_data : SpecificationWithRequestManagerIntegration {
		string _streamId = $"new_test_stream_{Guid.NewGuid()}";
		Event evt = new Event(Guid.NewGuid(), "SomethingHappened", true, Helper.UTF8NoBom.GetBytes("{Value:42}"), null);

		protected override IEnumerable<Message> WithInitialMessages() {
			var requestComplete = WaitFor<StorageMessage.RequestCompleted>();
			yield return new ClientMessage.TransactionStart(Guid.NewGuid(), Guid.NewGuid(), Envelope, true, _streamId, ExpectedVersion.Any, null);
			requestComplete.Wait(); 
			var resp = Envelope.Replies[0] as ClientMessage.TransactionStartCompleted;

			requestComplete = WaitFor<StorageMessage.RequestCompleted>();
			yield return new ClientMessage.TransactionWrite(Guid.NewGuid(), Guid.NewGuid(), Envelope, true, resp.TransactionId, new[] { evt }, null);
			requestComplete.Wait(); 			
			Produced.Clear();
			Envelope.Replies.Clear();

			yield return new ClientMessage.TransactionCommit(InternalCorrId, ClientCorrId, Envelope, true, resp.TransactionId, null);
			
		}

		protected override Message When() {
			return new CommitMessage.CommittedTo(3000);
		}

		[Test]
		public void successful_request_message_is_publised() {
			WaitFor<StorageMessage.RequestCompleted>().Wait();
			Produced.ContainsSingle<StorageMessage.RequestCompleted>(
				x => x.CorrelationId == InternalCorrId && x.Success);
		}

		[Test]
		public void the_envelope_is_replied_to_with_success() {
			WaitFor<StorageMessage.RequestCompleted>().Wait();
			AssertEx.IsOrBecomesTrue(() => Envelope.Replies.Count > 0);
			Assert.That(Envelope.Replies.ContainsSingle<ClientMessage.TransactionCommitCompleted>(
				x => x.CorrelationId == ClientCorrId && x.Result == OperationResult.Success),()=> $"Does not contain single successful TransactionCommitCompleted with id {ClientCorrId}");
		}
	}
}
