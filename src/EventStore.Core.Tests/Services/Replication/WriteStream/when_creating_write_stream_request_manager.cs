using System;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Tests.Fakes;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.WriteStream {
	[TestFixture]
	public class when_creating_write_stream_request_manager {
		protected static readonly TimeSpan PrepareTimeout = TimeSpan.FromMinutes(5);
		protected static readonly TimeSpan CommitTimeout = TimeSpan.FromMinutes(5);

		[Test]
		public void null_publisher_throws_argument_null_exception() {
			Assert.Throws<ArgumentNullException>(() =>
				new WriteStreamTwoPhaseRequestManager(null, 3, PrepareTimeout, CommitTimeout, false));
		}

		[Test]
		public void zero_prepare_ack_count_throws_argument_out_range() {
			Assert.Throws<ArgumentOutOfRangeException>(
				() => new WriteStreamTwoPhaseRequestManager(new FakePublisher(), 0, PrepareTimeout, CommitTimeout,
					false));
		}

		[Test]
		public void negative_prepare_ack_count_throws_argument_out_range() {
			Assert.Throws<ArgumentOutOfRangeException>(
				() => new WriteStreamTwoPhaseRequestManager(new FakePublisher(), -1, PrepareTimeout, CommitTimeout,
					false));
		}
	}
}
