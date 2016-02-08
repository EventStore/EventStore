using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Services;
using EventStore.Core.Services.UserManagement;
using EventStore.ClientAPI;
using NUnit.Framework;
using ILogger = EventStore.Common.Log.ILogger;


namespace EventStore.Core.Tests.Services.Storage.Scavenge
{
    [TestFixture]
	public class when_running_scavenge_from_storage_scavenger : SpecificationWithDirectoryPerTestFixture
    {
		private static readonly ILogger Log = LogManager.GetLoggerFor<when_running_scavenge_from_storage_scavenger>();
		private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
		private MiniNode _node;

		public override void TestFixtureSetUp()
        {
			base.TestFixtureSetUp();

			_node = new MiniNode(PathName, skipInitializeStandardUsersCheck: false);
			_node.Start();

			var scavengeMessage = new ClientMessage.ScavengeDatabase(new NoopEnvelope(), Guid.NewGuid(), SystemAccount.Principal);
			_node.Node.MainQueue.Publish(scavengeMessage);
        }

		[TearDown]
		public void TearDown()
		{
			_node.Shutdown();
		}

		[Test]
		public void should_create_scavenge_started_and_completed_link_to_events_on_index_stream_which_resolves_to_scavenge_stream() 
		{
			using(var conn = TestConnection.Create(_node.TcpEndPoint, TcpType.Normal, DefaultData.AdminCredentials))
			{
				conn.ConnectAsync().Wait();
				var countdown = new CountdownEvent(2);
				var events = new List<ResolvedEvent>();

				var subscription = conn.SubscribeToStreamFrom(
					SystemStreams.ScavengesStream, null, true,
                    (x, y) =>
					{
						events.Add(y);
						countdown.Signal();
					},
					_ => Log.Info("Processing events started."),
					(x, y, z) =>
					{
						Log.Info("Subscription dropped: {0}, {1}.", y, z);
					}
				);

				if (!countdown.Wait(Timeout))
				{
					Assert.Fail("Timeout expired while waiting for events.");
				}

				Assert.AreEqual(2, events.Count);

				var scavengeStartedEvent = events.FirstOrDefault(x=>x.Event.EventType == SystemEventTypes.ScavengeStarted);
				var scavengeCompletedEvent = events.FirstOrDefault(x=>x.Event.EventType == SystemEventTypes.ScavengeCompleted);

				Assert.IsNotNull(scavengeStartedEvent, "A scavenge started event was not written.");
				Assert.IsNotNull(scavengeCompletedEvent, "A scavenge completed event was not written.");

				Assert.AreEqual(scavengeStartedEvent.Event.EventStreamId, scavengeCompletedEvent.Event.EventStreamId, 
					string.Format("Scavenge started and completed events are not in the same stream. ScavengeStartedEvent Stream Id: {0}, ScavengeCompletedEvent Stream Id: {1}", 
				    	scavengeStartedEvent.Event.EventStreamId, scavengeCompletedEvent.Event.EventStreamId));

				subscription.Stop(Timeout);
			}
		}
    }
}
