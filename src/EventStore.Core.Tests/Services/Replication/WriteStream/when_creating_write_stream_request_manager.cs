using System;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Tests.Fakes;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.WriteStream {
	[TestFixture]
	public class when_creating_write_stream_request_manager {
		protected static readonly TimeSpan CommitTimeout = TimeSpan.FromMinutes(5);

		[Test]
		public void null_publisher_throws_argument_null_exception() {
			Assert.Throws<ArgumentNullException>(() =>
				new WriteStreamRequestManager(null,CommitTimeout, false));
		}		
	}
}
