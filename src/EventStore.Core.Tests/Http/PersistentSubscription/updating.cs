﻿using System;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Tests.Http.Users.users;
using NUnit.Framework;

namespace EventStore.Core.Tests.Http.PersistentSubscription {
	[TestFixture, Category("LongRunning")]
	class when_updating_a_subscription_without_permissions : with_admin_user {
		private HttpResponseMessage _response;

		protected override async Task Given() {
			_response = await MakeJsonPut(
				"/subscriptions/stream/groupname337",
				new {
					ResolveLinkTos = true
				}, _admin);
		}

		protected override async Task When() {
			SetDefaultCredentials(null);
			_response = await MakeJsonPost(
				"/subscriptions/stream/groupname337",
				new {
					ResolveLinkTos = true
				}, null);
		}

		[Test]
		public void returns_unauthorised() {
			Assert.AreEqual(HttpStatusCode.Unauthorized, _response.StatusCode);
		}
	}

	[TestFixture, Category("LongRunning")]
	class when_updating_a_non_existent_subscription_without_permissions : with_admin_user {
		private HttpResponseMessage _response;

		protected override Task Given() => Task.CompletedTask;

		protected override async Task When() {
			_response = await MakeJsonPost(
				"/subscriptions/stream/groupname3337",
				new {
					ResolveLinkTos = true
				}, new NetworkCredential("admin", "changeit"));
		}

		[Test]
		public void returns_not_found() {
			Assert.AreEqual(HttpStatusCode.NotFound, _response.StatusCode);
		}
	}

	[TestFixture, Category("LongRunning")]
	class when_updating_an_existing_subscription : with_admin_user {
		private HttpResponseMessage _response;
		private readonly string _groupName = Guid.NewGuid().ToString();
		private SubscriptionDropReason _droppedReason;
		private Exception _exception;
		private const string _stream = "stream";
		private AutoResetEvent _dropped = new AutoResetEvent(false);

		protected override async Task Given() {
			_response = await MakeJsonPut(
				string.Format("/subscriptions/{0}/{1}", _stream, _groupName),
				new {
					ResolveLinkTos = true
				}, DefaultData.AdminNetworkCredentials);
			SetupSubscription();
		}

		private void SetupSubscription() {
			_connection.ConnectToPersistentSubscription(_stream, _groupName, (x, y) => Task.CompletedTask,
				(sub, reason, ex) => {
					_droppedReason = reason;
					_exception = ex;
					_dropped.Set();
				}, DefaultData.AdminCredentials);
		}

		protected override async Task When() {
			_response = await MakeJsonPost(
				string.Format("/subscriptions/{0}/{1}", _stream, _groupName),
				new {
					ResolveLinkTos = true
				}, DefaultData.AdminNetworkCredentials);
		}

		[Test]
		public void returns_ok() {
			Assert.AreEqual(HttpStatusCode.OK, _response.StatusCode);
		}

		[Test]
		public void existing_subscriptions_are_dropped() {
			Assert.IsTrue(_dropped.WaitOne(TimeSpan.FromSeconds(5)));
			Assert.AreEqual(SubscriptionDropReason.UserInitiated, _droppedReason);
			Assert.IsNull(_exception);
		}

		[Test]
		public void location_header_is_present() {
			Assert.AreEqual(
				string.Format("http://{0}/subscriptions/{1}/{2}", _node.ExtHttpEndPoint, _stream, _groupName),
				_response.Headers.Location.ToString());
		}
	}
}
