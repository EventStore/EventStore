using System;
using System.Net;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.Tests.Integration;
using NUnit.Framework;

namespace EventStore.Core.Tests.Replication.ReadOnlyReplica {

	[TestFixture]
	[Category("LongRunning"), Ignore("Ignore Clustering Tests")]
	public class connecting_to_read_only_replica : specification_with_cluster {
		protected override MiniClusterNode CreateNode(int index, Endpoints endpoints, IPEndPoint[] gossipSeeds, bool wait = true) {
			var isReadOnly = index == 2;
			var node = new MiniClusterNode(
				PathName, index, endpoints.InternalTcp, endpoints.InternalTcpSec, endpoints.InternalHttp,
				endpoints.ExternalTcp,
				endpoints.ExternalTcpSec, endpoints.ExternalHttp, skipInitializeStandardUsersCheck: false,
				subsystems: new ISubsystem[] { }, gossipSeeds: gossipSeeds, inMemDb: false,
				readOnlyReplica: isReadOnly);
			if (wait && !isReadOnly)
				WaitIdle();
			return node;
		}

		protected override IEventStoreConnection CreateConnection() {
			var settings = ConnectionSettings.Create()
			.PerformOnAnyNode();
			return EventStoreConnection.Create(settings, _nodes[2].ExternalTcpEndPoint);
		}

		[Test]
		public void append_to_stream_should_fail_with_not_supported_exception() {
			const string stream = "append_to_stream_should_fail_with_not_supported_exception";
			var append = _conn.AppendToStreamAsync(stream, ExpectedVersion.Any, new[] { TestEvent.NewTestEvent() });
			Assert.That(() => append.Wait(),
				Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<OperationNotSupportedException>());
		}

		[Test]
		public void delete_stream_should_fail_with_not_supported_exception() {
			const string stream = "delete_stream_should_fail_with_not_supported_exception";
			var delete = _conn.DeleteStreamAsync(stream, ExpectedVersion.Any);
			Assert.That(() => delete.Wait(),
				Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<OperationNotSupportedException>());
		}

		[Test]
		public void start_transaction_should_fail_with_not_supported_exception() {
			const string stream = "start_transaction_should_fail_with_not_supported_exception";
			var start = _conn.StartTransactionAsync(stream, ExpectedVersion.Any);
			Assert.That(() => start.Wait(),
				Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<OperationNotSupportedException>());
		}
	}
}
