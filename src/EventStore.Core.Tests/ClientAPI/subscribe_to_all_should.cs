extern alias GrpcClient;
extern alias GrpcClientStreams;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using GrpcClientStreams::EventStore.Client;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI {
	[Category("ClientAPI"), Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class subscribe_to_all_should<TLogFormat, TStreamId> : SpecificationWithDirectory {
		private const int Timeout = 10000;

		private MiniNode<TLogFormat, TStreamId> _node;

		[SetUp]
		public override async Task SetUp() {
			await base.SetUp();
			_node = new MiniNode<TLogFormat, TStreamId>(PathName);
			await _node.Start();

			using (var connection = BuildConnection(_node)) {
				await connection.ConnectAsync();
				await connection.SetStreamMetadataAsync("$all", -1,
					new StreamMetadata(acl: new StreamAcl(readRole: SystemRoles.All)),
					DefaultData.AdminCredentials);
			}
		}

		[TearDown]
		public override async Task TearDown() {
			await _node.Shutdown();
			await base.TearDown();
		}

		protected virtual IEventStoreClient BuildConnection(MiniNode<TLogFormat, TStreamId> node) {
			return new GrpcEventStoreConnection(node.HttpEndPoint);
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
					return Task.CompletedTask;
				}, (s, r, e) => dropped.Signal()))
				using (await store.SubscribeToAllAsync(false, (s, x) => {
					appeared.Signal();
					return Task.CompletedTask;
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
