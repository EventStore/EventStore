using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Services;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.TransactionLog.Services;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI {
	[TestFixture, Category("ClientAPI"), Category("LongRunning")]
	public class subscribe_to_all_should : SpecificationWithDirectory {
		private const int Timeout = 10000;

		private MiniNode _node;

		[SetUp]
		public override async Task SetUp() {
			await base.SetUp();
			_node = new MiniNode(PathName, skipInitializeStandardUsersCheck: false);
			await _node.Start();

			using (var connection = BuildConnection(_node)) {
				await connection.ConnectAsync();
				await connection.SetStreamMetadataAsync("$all", -1,
					StreamMetadata.Build().SetReadRole(SystemRoles.All),
					new UserCredentials(SystemUsers.Admin, SystemUsers.DefaultAdminPassword));
			}
		}

		[TearDown]
		public override async Task TearDown() {
			await _node.Shutdown();
			await base.TearDown();
		}

		protected virtual IEventStoreConnection BuildConnection(MiniNode node) {
			return TestConnection.Create(node.TcpEndPoint);
		}

		[Test, Category("LongRunning")]
		public async Task allow_multiple_subscriptions() {
			const string stream = "subscribe_to_all_should_allow_multiple_subscriptions";
			using (var store = BuildConnection(_node)) {
				await store.ConnectAsync();
				var appeared = new CountdownEvent(2);
				var dropped = new CountdownEvent(2);

				using (await store.SubscribeToAllAsync(false, (s, x) => {
					appeared.Signal();
					return;
				}, (s, r, e) => dropped.Signal()))
				using (await store.SubscribeToAllAsync(false, (s, x) => {
					appeared.Signal();
					return;
				}, (s, r, e) => dropped.Signal())) {
					var create =
						await store.AppendToStreamAsync(stream, ExpectedVersion.NoStream, TestEvent.NewTestEvent());

					Assert.IsTrue(appeared.Wait(Timeout), "Appeared countdown event timed out.");
				}
			}
		}

		[Test, Category("LongRunning")]
		public async Task catch_deleted_events_as_well() {
			const string stream = "subscribe_to_all_should_catch_created_and_deleted_events_as_well";
			using (var store = BuildConnection(_node)) {
				await store.ConnectAsync();
				var appeared = new CountdownEvent(1);
				var dropped = new CountdownEvent(1);

				using (await store.SubscribeToAllAsync(false, (s, x) => {
					appeared.Signal();
					return Task.CompletedTask;
				},
					(s, r, e) => dropped.Signal())) {
					var delete = await store.DeleteStreamAsync(stream, ExpectedVersion.NoStream, hardDelete: true);

					Assert.IsTrue(appeared.Wait(Timeout), "Appeared countdown event didn't fire in time.");
				}
			}
		}
	}
}
