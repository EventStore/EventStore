using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.Core.Tests.Http.Users.users;
using NUnit.Framework;

namespace EventStore.Core.Tests.Http.PersistentSubscription {
	[TestFixture, Category("LongRunning")]
	class when_deleting_non_existing_subscription : with_admin_user {
		private HttpWebResponse _response;

		protected override void Given() {
		}

		protected override void When() {
			var req = CreateRequest("/subscriptions/stream/groupname158", "DELETE", _admin);
			_response = GetRequestResponse(req);
		}

		[Test]
		public void returns_not_found() {
			Assert.AreEqual(HttpStatusCode.NotFound, _response.StatusCode);
		}
	}

	[TestFixture, Category("LongRunning")]
	class when_deleting_an_existing_subscription : with_admin_user {
		private HttpWebResponse _response;

		protected override void Given() {
			_response = MakeJsonPut(
				"/subscriptions/stream/groupname156",
				new {
					ResolveLinkTos = true
				}, _admin);
		}

		protected override void When() {
			var req = CreateRequest("/subscriptions/stream/groupname156", "DELETE", _admin);
			_response = GetRequestResponse(req);
		}

		[Test]
		public void returns_ok() {
			Assert.AreEqual(HttpStatusCode.OK, _response.StatusCode);
		}
	}

	[TestFixture, Category("LongRunning")]
	class when_deleting_an_existing_subscription_without_permissions : with_admin_user {
		private HttpWebResponse _response;

		protected override void Given() {
			_response = MakeJsonPut(
				"/subscriptions/stream/groupname156",
				new {
					ResolveLinkTos = true
				}, _admin);
		}

		protected override void When() {
			var req = CreateRequest("/subscriptions/stream/groupname156", "DELETE");
			_response = GetRequestResponse(req);
		}

		[Test]
		public void returns_unauthorized() {
			Assert.AreEqual(HttpStatusCode.Unauthorized, _response.StatusCode);
		}
	}

	[TestFixture, Category("LongRunning")]
	class when_deleting_an_existing_subscription_with_subscribers : with_admin_user {
		private HttpWebResponse _response;
		private const string _stream = "astreamname";
		private readonly string _groupName = Guid.NewGuid().ToString();
		private SubscriptionDropReason _reason;
		private Exception _exception;
		private readonly AutoResetEvent _dropped = new AutoResetEvent(false);

		protected override void Given() {
			_response = MakeJsonPut(
				string.Format("/subscriptions/{0}/{1}", _stream, _groupName),
				new {
					ResolveLinkTos = true
				}, _admin);
			_connection.ConnectToPersistentSubscription(_stream, _groupName, (x, y) => Task.CompletedTask,
				(sub, reason, e) => {
					_dropped.Set();
					_reason = reason;
					_exception = e;
				});
		}

		protected override void When() {
			var req = CreateRequest(string.Format("/subscriptions/{0}/{1}", _stream, _groupName), "DELETE", _admin);
			_response = GetRequestResponse(req);
		}

		[Test]
		public void returns_ok() {
			Assert.AreEqual(HttpStatusCode.OK, _response.StatusCode);
		}

		[Test]
		public void the_subscription_is_dropped() {
			Assert.IsTrue(_dropped.WaitOne(TimeSpan.FromSeconds(5)));
			Assert.AreEqual(SubscriptionDropReason.UserInitiated, _reason);
			Assert.IsNull(_exception);
		}
	}
}
