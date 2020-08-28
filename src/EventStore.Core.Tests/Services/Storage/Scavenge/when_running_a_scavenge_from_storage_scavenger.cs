using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Services;
using EventStore.Core.Services.UserManagement;
using EventStore.ClientAPI;
using NUnit.Framework;
using ILogger = Serilog.ILogger;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog.Services;

namespace EventStore.Core.Tests.Services.Storage.Scavenge {
	[TestFixture]
	public class when_running_scavenge_from_storage_scavenger : SpecificationWithDirectoryPerTestFixture {
		private static readonly ILogger Log = Serilog.Log.ForContext<when_running_scavenge_from_storage_scavenger>();
		private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
		private MiniNode _node;
		private List<ResolvedEvent> _result;

		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();

			_node = new MiniNode(PathName, skipInitializeStandardUsersCheck: false);
			await _node.Start();

			var scavengeMessage =
				new ClientMessage.ScavengeDatabase(new NoopEnvelope(), Guid.NewGuid(), SystemAccounts.System, 0, 1);
			_node.Node.MainQueue.Publish(scavengeMessage);

			try {
				await When().WithTimeout();
			} catch (Exception ex) {
				throw new Exception("When Failed", ex);
			}
		}

		[TearDown]
		public async Task TearDown() {
			await _node.Shutdown();
		}

		public async Task When() {
			using (var conn = TestConnection.Create(_node.TcpEndPoint, TcpType.Normal, DefaultData.AdminCredentials)) {
				await conn.ConnectAsync();
				var countdown = new CountdownEvent(2);
				_result = new List<ResolvedEvent>();

				conn.SubscribeToStreamFrom(SystemStreams.ScavengesStream, null, CatchUpSubscriptionSettings.Default,
					(x, y) => {
						_result.Add(y);
						countdown.Signal();
						return Task.CompletedTask;
					},
					_ => Log.Information("Processing events started."),
					(x, y, z) => { Log.Information("Subscription dropped: {0}, {1}.", y, z); }
				);

				if (!countdown.Wait(Timeout)) {
					Assert.Fail("Timeout expired while waiting for events.");
				}
			}
		}

		[Test]
		public void should_create_scavenge_started_event_on_index_stream() {
			var scavengeStartedEvent =
				_result.FirstOrDefault(x => x.Event.EventType == SystemEventTypes.ScavengeStarted);
			Assert.IsNotNull(scavengeStartedEvent);
		}

		[Test]
		public void should_create_scavenge_completed_event_on_index_stream() {
			var scavengeCompletedEvent =
				_result.FirstOrDefault(x => x.Event.EventType == SystemEventTypes.ScavengeCompleted);
			Assert.IsNotNull(scavengeCompletedEvent);
		}

		[Test]
		public void should_link_started_and_completed_events_to_the_same_stream() {
			var scavengeStartedEvent =
				_result.FirstOrDefault(x => x.Event.EventType == SystemEventTypes.ScavengeStarted);
			var scavengeCompletedEvent =
				_result.FirstOrDefault(x => x.Event.EventType == SystemEventTypes.ScavengeCompleted);
			Assert.AreEqual(scavengeStartedEvent.Event.EventStreamId, scavengeCompletedEvent.Event.EventStreamId);
		}
	}
}
