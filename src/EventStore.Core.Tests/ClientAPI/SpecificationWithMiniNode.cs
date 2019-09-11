using System;
using System.Net;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI {
	public abstract class SpecificationWithMiniNode : SpecificationWithDirectoryPerTestFixture {
		protected MiniNode _node;
		protected IEventStoreConnection _conn;
		protected IPEndPoint _HttpEndPoint;
		protected virtual TimeSpan Timeout { get; } = TimeSpan.FromSeconds(3);

		protected virtual Task Given() => Task.CompletedTask;

		protected abstract Task When();

		protected virtual IEventStoreConnection BuildConnection(MiniNode node) {
			return TestConnection.Create(node.TcpEndPoint);
		}

		[OneTimeSetUp]
		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();
			_node = new MiniNode(PathName, skipInitializeStandardUsersCheck: false);
			await _node.Start();
			_HttpEndPoint = _node.ExtHttpEndPoint;
			_conn = BuildConnection(_node);
			await _conn.ConnectAsync();

			try {
				await Given().WithTimeout(Timeout);
			} catch (Exception ex) {
				throw new Exception("Given Failed", ex);
			}

			try {
				await When().WithTimeout(Timeout);
			} catch (Exception ex) {
				throw new Exception("When Failed", ex);
			}
		}

		[OneTimeTearDown]
		public override async Task TestFixtureTearDown() {
			_conn.Close();
			await _node.Shutdown();
			await base.TestFixtureTearDown();
		}
	}
}
