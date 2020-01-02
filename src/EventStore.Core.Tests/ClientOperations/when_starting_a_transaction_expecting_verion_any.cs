using System;
using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Integration.ClientRequests {
	public class when_starting_a_transaction_expecting_version_any : SpecificationWithRequestManagerIntegration {
		string _streamId = $"new_test_stream_{Guid.NewGuid()}";

		protected override IEnumerable<Message> WithInitialMessages() {
			yield break;
		}

		protected override Message When() {
			return new ClientMessage.TransactionStart(InternalCorrId, ClientCorrId, Envelope, true, _streamId, ExpectedVersion.Any, null);
		}

		[Test]
		public void successful_request_message_is_publised() {
			AssertEx.IsOrBecomesTrue(() => Produced.ContainsN<StorageMessage.RequestCompleted>(1,
				x => x.CorrelationId == InternalCorrId && x.Success));
		}

		[Test]
		public void the_envelope_is_replied_to_with_success() {
			AssertEx.IsOrBecomesTrue(() => Envelope.Replies.Count > 0);
			Assert.That(Envelope.Replies.ContainsSingle<ClientMessage.TransactionStartCompleted>(
				x => x.CorrelationId == ClientCorrId && x.Result == OperationResult.Success));
		}
	}
}
