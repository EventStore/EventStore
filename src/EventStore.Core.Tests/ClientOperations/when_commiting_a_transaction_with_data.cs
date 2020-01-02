using System;
using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.Core.Tests.Integration.ClientRequests {
	public class when_commiting_a_transaction_with_data : SpecificationWithRequestManagerIntegration {
		string _streamId = $"new_test_stream_{Guid.NewGuid()}";
		Event evt = new Event(Guid.NewGuid(), "SomethingHappened", true, Helper.UTF8NoBom.GetBytes("{Value:42}"), null);
		long _transactionId;

		protected override IEnumerable<Message> WithInitialMessages() {
			var requestComplete = WaitForNext<StorageMessage.RequestCompleted>();
			yield return new ClientMessage.TransactionStart(Guid.NewGuid(), Guid.NewGuid(), Envelope, true, _streamId, ExpectedVersion.Any, null);
			requestComplete.Wait();
			var resp = Envelope.Replies[0] as ClientMessage.TransactionStartCompleted;
			_transactionId = resp.TransactionId;
			requestComplete = WaitForNext<StorageMessage.RequestCompleted>();
			yield return new ClientMessage.TransactionWrite(Guid.NewGuid(), Guid.NewGuid(), Envelope, true, _transactionId, new[] { evt }, null);
			requestComplete.Wait();
			Produced.Clear();
			Envelope.Replies.Clear();
			yield break;
		}

		protected override Message When() {
			return new ClientMessage.TransactionCommit(InternalCorrId, ClientCorrId, Envelope, true, _transactionId, null);
		}

		[Test]
		public void successful_request_message_is_published() {
			WaitForNext<StorageMessage.RequestCompleted>().Wait();
			Produced.ContainsSingle<StorageMessage.RequestCompleted>(
				x => x.CorrelationId == InternalCorrId && x.Success);
		}

		[Test]
		public void the_envelope_is_replied_to_with_success() {
			AssertEx.IsOrBecomesTrue(() => Envelope.Replies.Count > 0);
			Assert.That(Envelope.Replies.ContainsSingle<ClientMessage.TransactionCommitCompleted>(
				x => x.CorrelationId == ClientCorrId && x.Result == OperationResult.Success), () => $"Does not contain single successful TransactionCommitCompleted with id {ClientCorrId}");
		}
	}
}
