using System;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Commit;
using EventStore.Core.Services.RequestManager;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.RequestManager {
	[TestFixture]
	public partial class when_creating_stream_request_manager {
		protected static readonly IPublisher Publisher = new InMemoryBus("test");
		protected static readonly TimeSpan CommitTimeout = TimeSpan.FromMinutes(5);
		protected static readonly IEnvelope Envelope = new NoopEnvelope();
		protected static readonly Guid InternalCorrId = Guid.NewGuid();
		protected static readonly Guid ClientCorrId = Guid.NewGuid();
		protected static readonly long ExpectedVerion = 1;



		[Test]
		public void null_publisher_throws_argument_null_exception() {
			Assert.Throws<ArgumentNullException>(() =>
				new FakeRequestManager(
					null,
					CommitTimeout,
					Envelope,
					InternalCorrId,
					ClientCorrId,
					ExpectedVerion,
					new CommitSource()));
		}
		[Test]
		public void null_envelope_throws_argument_null_exception() {
			Assert.Throws<ArgumentNullException>(() =>
				new FakeRequestManager(
					Publisher,
					CommitTimeout,
					null,
					InternalCorrId,
					ClientCorrId,
					ExpectedVerion,
					new CommitSource()));
		}
		[Test]
		public void internal_corrId_empty_guid_throws_argument_exception() {
			Assert.Throws<ArgumentException>(() =>
				new FakeRequestManager(
					Publisher,
					CommitTimeout,
					Envelope,
					Guid.Empty,
					ClientCorrId,
					ExpectedVerion,
					new CommitSource()));
		}
		[Test]
		public void client_corrId_empty_guid_throws_argument_exception() {
			Assert.Throws<ArgumentException>(() =>
				new FakeRequestManager(
					Publisher,
					CommitTimeout,
					Envelope,
					InternalCorrId,
					Guid.Empty,
					ExpectedVerion,
					new CommitSource()));
		}
		
		[Test]
		public void empty_commit_source_throws_null_argument_exception() {
			Assert.Throws<ArgumentNullException>(() =>
				new FakeRequestManager(
					Publisher,
					CommitTimeout,
					Envelope,
					InternalCorrId,
					ClientCorrId,
					ExpectedVerion,
					null));
		}
	}
}
