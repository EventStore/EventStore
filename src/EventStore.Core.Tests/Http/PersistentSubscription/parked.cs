﻿using System;
using System.Collections.Concurrent;
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

	class when_replaying_one_all_parked_message : with_subscription_having_events {
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
			_node.Node.MainBus.Subscribe(new AdHocHandler<StorageMessage.CommitIndexed>(Handle));

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

		private void Handle(StorageMessage.CommitIndexed msg) {
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


	class when_replaying_multiple_all_parked_messages : with_subscription_having_events {
		private List<Guid> _parkedEventIds = new List<Guid>();
		private List<Guid> _receivedEventId = new List<Guid>();

		private string _subscriptionParkedStream;
		private ConcurrentDictionary<Guid, bool> _writeCorrelationId = new ConcurrentDictionary<Guid, bool>();
		private TaskCompletionSource<bool> _eventParked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		
		protected override async Task Given() {
			_connection.Close();
			_connection.Dispose();
			NumberOfEventsToCreate = 3;
			await base.Given();

			_subscriptionParkedStream =
				"$persistentsubscription-" + TestStream.Substring(9) + "::" + GroupName + "-parked";

			// Subscribe to the writes to ensure the parked message has been written
			_node.Node.MainBus.Subscribe(new AdHocHandler<StorageMessage.WritePrepares>(Handle));
			_node.Node.MainBus.Subscribe(new AdHocHandler<StorageMessage.CommitIndexed>(Handle));

			// park 3 events
			for (int i = 0; i < NumberOfEventsToCreate; i++) {
				for (var attempt = 0; attempt < 10; attempt++) {
					var json = await GetJson2<JObject>(
						SubscriptionPath + "/1", "embed=rich",
						ContentType.CompetingJson,
						_admin);

					Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);

					var entries = json != null ? json["entries"].ToList() : new List<JToken>();
					if (entries.Count == 1) {
						//Park the message
						_parkedEventIds.Add(Guid.Parse(entries[0]["eventId"].ToString()));
						var nackLink = entries[0]["links"][3]["uri"] + "?action=park";
						var response = await MakePost(nackLink, _admin);
						Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode);
						break;
					}

					Console.WriteLine("Received no entries. Attempt {0} of 10", attempt + 1);
					await Task.Delay(TimeSpan.FromSeconds(1));
				}
			}
		}

		private void Handle(StorageMessage.WritePrepares msg) {
			if (msg.EventStreamId == _subscriptionParkedStream) {
				_writeCorrelationId.TryAdd(msg.CorrelationId, true);
			}
		}

		private void Handle(StorageMessage.CommitIndexed msg) {
			if (_writeCorrelationId.TryUpdate(msg.CorrelationId, false, true) && _writeCorrelationId.Count(kvp => !kvp.Value) == NumberOfEventsToCreate) {
				_eventParked.TrySetResult(true);
			}
		}

		protected override async Task When() {
			await _eventParked.Task.WithTimeout(10000);

			// replay all
			var response = await MakePost(SubscriptionPath + "/replayParked", _admin);
			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

			for (int i = 0; i < NumberOfEventsToCreate; i++) {
				for (var attempt = 0; attempt < 10; attempt++) {
					var json = await GetJson2<JObject>(
						SubscriptionPath + "/1", "embed=rich",
						ContentType.CompetingJson,
						_admin);

					Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);

					var entries = json != null ? json["entries"].ToList() : new List<JToken>();
					if (entries.Count == 1) {
						_receivedEventId.Add(Guid.Parse(entries[0]["eventId"].ToString()));
						break;
					}

					Console.WriteLine("Received no entries. Attempt {0} of 10", attempt + 1);
					await Task.Delay(TimeSpan.FromSeconds(1));
				}
			}
		}

		[Test]
		public void should_receive_the_replayed_event() {
			Assert.AreEqual(_parkedEventIds[0], _receivedEventId[0]);
			Assert.AreEqual(_parkedEventIds[1], _receivedEventId[1]);
			Assert.AreEqual(_parkedEventIds[2], _receivedEventId[2]);
		}
	}
	
	
	class when_replaying_multiple_some_parked_messages : with_subscription_having_events {
		private List<Guid> _parkedEventIds = new List<Guid>();
		private List<Guid> _receivedEventId = new List<Guid>();

		private string _subscriptionParkedStream;
		private ConcurrentDictionary<Guid, bool> _writeCorrelationId = new ConcurrentDictionary<Guid, bool>();
		private TaskCompletionSource<bool> _eventParked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		protected override async Task Given() {
			_connection.Close();
			_connection.Dispose();
			NumberOfEventsToCreate = 3;
			await base.Given();

			_subscriptionParkedStream =
				"$persistentsubscription-" + TestStream.Substring(9) + "::" + GroupName + "-parked";

			// Subscribe to the writes to ensure the parked message has been written
			_node.Node.MainBus.Subscribe(new AdHocHandler<StorageMessage.WritePrepares>(Handle));
			_node.Node.MainBus.Subscribe(new AdHocHandler<StorageMessage.CommitIndexed>(Handle));

			// park 3 events
			for (int i = 0; i < NumberOfEventsToCreate; i++) {
				for (var attempt = 0; attempt < 10; attempt++) {
					var json = await GetJson2<JObject>(
						SubscriptionPath + "/1", "embed=rich",
						ContentType.CompetingJson,
						_admin);

					Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);

					var entries = json != null ? json["entries"].ToList() : new List<JToken>();
					if (entries.Count == 1) {
						//Park the message
						_parkedEventIds.Add(Guid.Parse(entries[0]["eventId"].ToString()));
						var nackLink = entries[0]["links"][3]["uri"] + "?action=park";
						var response = await MakePost(nackLink, _admin);
						Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode);
						break;
					}

					Console.WriteLine("Received no entries. Attempt {0} of 10", attempt + 1);
					await Task.Delay(TimeSpan.FromSeconds(1));
				}
			}
		}

		private void Handle(StorageMessage.WritePrepares msg) {
			if (msg.EventStreamId == _subscriptionParkedStream) {
				_writeCorrelationId.TryAdd(msg.CorrelationId, true);
			}
		}

		private void Handle(StorageMessage.CommitIndexed msg) {
			if (_writeCorrelationId.TryUpdate(msg.CorrelationId, false, true) && _writeCorrelationId.Count(kvp => !kvp.Value) == NumberOfEventsToCreate) {
				_eventParked.TrySetResult(true);
			}
		}

		protected override async Task When() {
			await _eventParked.Task.WithTimeout(10000);

			// only replay 2 of the 3 messages
			var response = await MakePost(SubscriptionPath + "/replayParked", _admin, "stopAt=2");
			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

			for (int i = 0; i < 2; i++) {
				for (var attempt = 0; attempt < 10; attempt++) {
					var json = await GetJson2<JObject>(
						SubscriptionPath + "/1", "embed=rich",
						ContentType.CompetingJson,
						_admin);

					Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);

					var entries = json != null ? json["entries"].ToList() : new List<JToken>();
					if (entries.Count == 1) {
						_receivedEventId.Add(Guid.Parse(entries[0]["eventId"].ToString()));
						break;
					}

					Console.WriteLine("Received no entries. Attempt {0} of 10", attempt + 1);
					await Task.Delay(TimeSpan.FromSeconds(1));
				}
			}
		}

		[Test]
		public void should_receive_the_replayed_event() {
			Assert.AreEqual(2, _receivedEventId.Count);
			Assert.AreEqual(3, _parkedEventIds.Count);
			Assert.AreEqual(_parkedEventIds[0], _receivedEventId[0]);
			Assert.AreEqual(_parkedEventIds[1], _receivedEventId[1]);
		}
	}
}
