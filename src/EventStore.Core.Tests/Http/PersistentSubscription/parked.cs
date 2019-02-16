using System;
using System.Net;
using System.Text.RegularExpressions;
using EventStore.Core.Tests.Http.Users.users;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json.Linq;
using System.Linq;
using HttpStatusCode = System.Net.HttpStatusCode;
using EventStore.Transport.Http;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Common;
using EventStore.Core.Data;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Messages;

namespace EventStore.Core.Tests.Http.PersistentSubscription {
	class when_parking_a_message : with_subscription_having_events {
		private string _nackLink;
		private Guid _eventIdToPark;
		private Guid _parkedEventId;
		private List<JToken> _entries;
		private AutoResetEvent _eventParked = new AutoResetEvent(false);

		protected override void Given() {
			NumberOfEventsToCreate = 1;
			base.Given();
			var json = GetJson2<JObject>(
				SubscriptionPath + "/1", "embed=rich",
				ContentType.CompetingJson,
				_admin);
			Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
			_entries = json != null ? json["entries"].ToList() : new List<JToken>();
			_nackLink = _entries[0]["links"][3]["uri"].ToString() + "?action=park";
			_eventIdToPark = Guid.Parse(_entries[0]["eventId"].ToString());
		}

		protected override void When() {
			var parkedStreamId = String.Format("$persistentsubscription-{0}::{1}-parked", TestStreamName, GroupName);

			_connection.SubscribeToStreamAsync(parkedStreamId, true, (x, y) => {
					_parkedEventId = y.Event.EventId;
					_eventParked.Set();
					return Task.CompletedTask;
				},
				(x, y, z) => { },
				DefaultData.AdminCredentials).Wait();

			var response = MakePost(_nackLink, _admin);
			Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode);
		}

		[Test]
		public void should_have_parked_the_event() {
			Assert.IsTrue(_eventParked.WaitOne(TimeSpan.FromSeconds(5)));
			Assert.AreEqual(_eventIdToPark, _parkedEventId);
		}
	}

	class when_replaying_parked_message : with_subscription_having_events {
		private string _nackLink;
		private Guid _eventIdToPark;
		private Guid _receivedEventId;

		private string _subscriptionParkedStream;
		private Guid _writeCorrelationId;
		private ManualResetEvent _eventParked = new ManualResetEvent(false);

		protected override void Given() {
			_connection.Close();
			_connection.Dispose();
			NumberOfEventsToCreate = 1;
			base.Given();

			_subscriptionParkedStream =
				"$persistentsubscription-" + TestStream.Substring(9) + "::" + GroupName + "-parked";

			// Subscribe to the writes to ensure the parked message has been written
			_node.Node.MainBus.Subscribe(new AdHocHandler<StorageMessage.WritePrepares>(Handle));
			_node.Node.MainBus.Subscribe(new AdHocHandler<StorageMessage.CommitReplicated>(Handle));

			var json = GetJson2<JObject>(
				SubscriptionPath + "/1", "embed=rich",
				ContentType.CompetingJson,
				_admin);

			Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);

			var _entries = json != null ? json["entries"].ToList() : new List<JToken>();
			_nackLink = _entries[0]["links"][3]["uri"].ToString() + "?action=park";
			_eventIdToPark = Guid.Parse(_entries[0]["eventId"].ToString());

			//Park the message
			var response = MakePost(_nackLink, _admin);
			Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode);
		}

		private void Handle(StorageMessage.WritePrepares msg) {
			if (msg.EventStreamId == _subscriptionParkedStream) {
				_writeCorrelationId = msg.CorrelationId;
			}
		}

		private void Handle(StorageMessage.CommitReplicated msg) {
			if (msg.CorrelationId == _writeCorrelationId) {
				_eventParked.Set();
			}
		}

		protected override void When() {
			if (!_eventParked.WaitOne(TimeSpan.FromSeconds(10))) {
				Assert.Fail("Timed out waiting for event to be written to the parked stream");
			}

			var response = MakePost(SubscriptionPath + "/replayParked", _admin);
			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

			for (var i = 0; i < 10; i++) {
				var json = GetJson2<JObject>(
					SubscriptionPath + "/1", "embed=rich",
					ContentType.CompetingJson,
					_admin);

				Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);

				var entries = json != null ? json["entries"].ToList() : new List<JToken>();
				if (entries.Count != 0) {
					_receivedEventId = Guid.Parse(entries[0]["eventId"].ToString());
					break;
				}

				Console.WriteLine("Received no entries. Attempt {0} of 10", i + 1);
				Thread.Sleep(TimeSpan.FromSeconds(1));
			}
		}

		[Test]
		public void should_receive_the_replayed_event() {
			Assert.AreEqual(_eventIdToPark, _receivedEventId);
		}
	}
}
