using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Core.Tests.ClientAPI {
	[TestFixture, Ignore("Very long running")]
	[Category("LongRunning"), Category("ClientAPI")]
	public class catchup_subscription_handles_small_batch_sizes : SpecificationWithDirectoryPerTestFixture {
		private MiniNode _node;
		private string _streamName = "TestStream";
		private CatchUpSubscriptionSettings _settings;
		private IEventStoreConnection _conn;

		[OneTimeSetUp]
		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();
			_node = new MiniNode(PathName, inMemDb: true);
			_node.Start();

			_conn = BuildConnection(_node);
			_conn.ConnectAsync().Wait();
			//Create 80000 events
			for (var i = 0; i < 80; i++) {
				_conn.AppendToStreamAsync(_streamName, ExpectedVersion.Any, CreateThousandEvents()).Wait();
			}

			_settings = new CatchUpSubscriptionSettings(100, 1, false, true, String.Empty);
		}

		private EventData[] CreateThousandEvents() {
			var events = new List<EventData>();
			for (var i = 0; i < 1000; i++) {
				events.Add(new EventData(Guid.NewGuid(), "testEvent", true,
					Encoding.UTF8.GetBytes("{ \"Foo\":\"Bar\" }"), null));
			}

			return events.ToArray();
		}

		[OneTimeTearDown]
		public override void TestFixtureTearDown() {
			_conn.Dispose();
			_node.Shutdown();
			base.TestFixtureTearDown();
		}

		protected virtual IEventStoreConnection BuildConnection(MiniNode node) {
			return TestConnection.Create(node.TcpEndPoint);
		}

		[Test]
		public void CatchupSubscriptionToAllHandlesManyEventsWithSmallBatchSize() {
			var mre = new ManualResetEvent(false);
			_conn.SubscribeToAllFrom(null, _settings, (sub, evnt) => {
				if (evnt.OriginalEventNumber % 1000 == 0) {
					Console.WriteLine("Processed {0} events", evnt.OriginalEventNumber);
				}

				return Task.CompletedTask;
			}, (sub) => { mre.Set(); }, null, new UserCredentials("admin", "changeit"));

			if (!mre.WaitOne(TimeSpan.FromMinutes(10)))
				Assert.Fail("Timed out waiting for test to complete");
		}

		[Test]
		public void CatchupSubscriptionToStreamHandlesManyEventsWithSmallBatchSize() {
			var mre = new ManualResetEvent(false);
			_conn.SubscribeToStreamFrom(_streamName, null, _settings, (sub, evnt) => {
				if (evnt.OriginalEventNumber % 1000 == 0) {
					Console.WriteLine("Processed {0} events", evnt.OriginalEventNumber);
				}

				return Task.CompletedTask;
			}, (sub) => { mre.Set(); }, null, new UserCredentials("admin", "changeit"));

			if (!mre.WaitOne(TimeSpan.FromMinutes(10)))
				Assert.Fail("Timed out waiting for test to complete");
		}
	}
}
