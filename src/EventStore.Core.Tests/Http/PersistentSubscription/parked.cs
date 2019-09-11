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
		private readonly TaskCompletionSource<bool> _eventParked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		protected override async Task Given() {
			NumberOfEventsToCreate = 1;
			await base.Given();
			var json = await GetJson2<JObject>(
				SubscriptionPath + "/1", "embed=rich",
				ContentType.CompetingJson,
				_admin);
			Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
			_entries = json != null ? json["entries"].ToList() : new List<JToken>();
			_nackLink = _entries[0]["links"][3]["uri"].ToString() + "?action=park";
			_eventIdToPark = Guid.Parse(_entries[0]["eventId"].ToString());
		}

		protected override async Task When() {
			var parkedStreamId = String.Format("$persistentsubscription-{0}::{1}-parked", TestStreamName, GroupName);

			await _connection.SubscribeToStreamAsync(parkedStreamId, true, (x, y) => {
				_parkedEventId = y.Event.EventId;
				_eventParked.TrySetResult(true);
				return Task.CompletedTask;
			},
				(x, y, z) => { },
				DefaultData.AdminCredentials);

			var response = await MakePost(_nackLink, _admin);
			Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode);
		}

		[Test]
		public async Task should_have_parked_the_event() {
			await _eventParked.Task.WithTimeout(5000);
			Assert.AreEqual(_eventIdToPark, _parkedEventId);
		}
	}

	class when_replaying_parked_message : with_subscription_having_events {
		private string _nackLink;
		private Guid _eventIdToPark;
		private Guid _receivedEventId;

		private string _subscriptionParkedStream;
		private Guid _writeCorrelationId;
		private TaskCompletionSource<bool> _eventParked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		protected override async Task Given() {
			_connection.Close();
			_connection.Dispose();
			NumberOfEventsToCreate = 1;
			await base.Given();

			_subscriptionParkedStream =
				"$persistentsubscription-" + TestStream.Substring(9) + "::" + GroupName + "-parked";

			// Subscribe to the writes to ensure the parked message has been written
			_node.Node.MainBus.Subscribe(new AdHocHandler<StorageMessage.WritePrepares>(Handle));
			_node.Node.MainBus.Subscribe(new AdHocHandler<StorageMessage.CommitReplicated>(Handle));

			var json = await GetJson2<JObject>(
				SubscriptionPath + "/1", "embed=rich",
				ContentType.CompetingJson,
				_admin);

			Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);

			var _entries = json != null ? json["entries"].ToList() : new List<JToken>();
			_nackLink = _entries[0]["links"][3]["uri"].ToString() + "?action=park";
			_eventIdToPark = Guid.Parse(_entries[0]["eventId"].ToString());

			//Park the message
			var response = await MakePost(_nackLink, _admin);
			Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode);
		}

		private void Handle(StorageMessage.WritePrepares msg) {
			if (msg.EventStreamId == _subscriptionParkedStream) {
				_writeCorrelationId = msg.CorrelationId;
			}
		}

		private void Handle(StorageMessage.CommitReplicated msg) {
			if (msg.CorrelationId == _writeCorrelationId) {
				_eventParked.TrySetResult(true);
			}
		}

		protected override async Task When() {
			await _eventParked.Task.WithTimeout(10000);

			var response = await MakePost(SubscriptionPath + "/replayParked", _admin);
			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

			for (var i = 0; i < 10; i++) {
				var json = await GetJson2<JObject>(
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
				await Task.Delay(TimeSpan.FromSeconds(1));
			}
		}

		[Test]
		public void should_receive_the_replayed_event() {
			Assert.AreEqual(_eventIdToPark, _receivedEventId);
		}
	}
}
