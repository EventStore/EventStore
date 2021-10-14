using System;
using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Fakes;
using NUnit.Framework;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Tests.Bus.Helpers;

namespace EventStore.Core.Tests.Services.RequestManagement.WriteStreamMgr {
	[TestFixture]
	public class when_write_stream_gets_timeout_after_local_commit : RequestManagerSpecification<WriteEvents> {
		protected override WriteEvents OnManager(FakePublisher publisher) {
			return new WriteEvents(
				publisher,
				1,
				CommitTimeout,
				Envelope,
				InternalCorrId,
				ClientCorrId,
				"test123",
				ExpectedVersion.Any,
				new[] { DummyEvent() },
				CommitSource);
		}

		protected override IEnumerable<Message> WithInitialMessages() {
			yield return new StorageMessage.CommitIndexed(InternalCorrId, 1, 1, 0, 0);
		}

		protected override Message When() {
			Manager.RequestTimedOut();
			return new TestMessage();
		}

		[Test]
		public void request_completed_as_failed_published() {
			AssertEx.IsOrBecomesTrue(() => Produced.Count == 1, TimeSpan.FromSeconds(1));
			Assert.AreEqual(false, ((StorageMessage.RequestCompleted)Produced[0]).Success);
		}

		[Test]
		public void the_envelope_is_replied_to() {
			AssertEx.IsOrBecomesTrue(() => Envelope.Replies.Count == 1, TimeSpan.FromSeconds(1));

		}
	}
}
