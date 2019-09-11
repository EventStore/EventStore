using System.Net;
using EventStore.Core.Tests.Http.Users.users;
using NUnit.Framework;
using System.Collections.Generic;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace EventStore.Core.Tests.Http.PersistentSubscription {
	[TestFixture, Category("LongRunning")]
	class when_creating_a_subscription : with_admin_user {
		private HttpResponseMessage _response;

		protected override Task Given() => Task.CompletedTask;

		protected override async Task When() {
			_response = await MakeJsonPut(
				"/subscriptions/stream/groupname334",
				new {
					ResolveLinkTos = true
				}, _admin);
		}

		[OneTimeTearDown]
		public void TearDown() {
			_response?.Dispose();
		}

		[Test]
		public void returns_created() {
			Assert.AreEqual(HttpStatusCode.Created, _response.StatusCode);
		}

		[Test]
		public void returns_location_header() {
			Assert.AreEqual("http://" + _node.ExtHttpEndPoint + "/subscriptions/stream/groupname334",
				_response.Headers.Location.ToString());
		}
	}

	[TestFixture, Category("LongRunning")]
	class when_creating_a_subscription_with_query_params : with_admin_user {
		private HttpResponseMessage _response;

		protected override Task Given() => Task.CompletedTask;

		protected override async Task When() {
			_response = await MakeJsonPut(
				"/subscriptions/stream/groupname334",
				new {
					ResolveLinkTos = true
				}, _admin, "testing=test");
		}

		[OneTimeTearDown]
		public void TearDown() {
			_response.Dispose();
		}

		[Test]
		public void returns_created() {
			Assert.AreEqual(HttpStatusCode.Created, _response.StatusCode);
		}

		[Test]
		public void returns_location_header() {
			Assert.AreEqual("http://" + _node.ExtHttpEndPoint + "/subscriptions/stream/groupname334",
				_response.Headers.Location.ToString());
		}
	}

	[TestFixture, Category("LongRunning")]
	class when_creating_a_subscription_without_permissions : with_admin_user {
		private HttpResponseMessage _response;

		protected override Task Given() => Task.CompletedTask;

		protected override async Task When() {
			SetDefaultCredentials(null);
			_response = await MakeJsonPut(
				"/subscriptions/stream/groupname337",
				new {
					ResolveLinkTos = true
				});
		}

		[OneTimeTearDown]
		public void TearDown() {
			_response?.Dispose();
		}

		[Test]
		public void returns_unauthorised() {
			Assert.AreEqual(HttpStatusCode.Unauthorized, _response.StatusCode);
		}
	}

	[TestFixture, Category("LongRunning")]
	class when_creating_a_duplicate_subscription : with_admin_user {
		private HttpResponseMessage _response;

		protected override async Task Given() {
			_response = await MakeJsonPut(
				"/subscriptions/stream/groupname453",
				new {
					ResolveLinkTos = true
				}, _admin);
		}

		protected override async Task When() {
			_response = await MakeJsonPut(
				"/subscriptions/stream/groupname453",
				new {
					ResolveLinkTos = true
				}, _admin);
		}

		[OneTimeTearDown]
		public void TearDown() {
			_response?.Dispose();
		}

		[Test]
		public void returns_conflict() {
			Assert.AreEqual(HttpStatusCode.Conflict, _response.StatusCode);
		}
	}

	[TestFixture, Category("LongRunning")]
	class when_creating_a_subscription_with_bad_config : with_admin_user {
		protected List<object> Events;
		protected string SubscriptionPath;
		protected string GroupName;
		protected HttpResponseMessage Response;

		protected override async Task Given() {
			Events = new List<object> {
				new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {A = "1"}},
				new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {B = "2"}},
				new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {C = "3"}},
				new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {D = "4"}}
			};

			Response = await MakeArrayEventsPost(
				TestStream,
				Events,
				_admin);
			Assert.AreEqual(HttpStatusCode.Created, Response.StatusCode);
		}

		protected override async Task When() {
			GroupName = Guid.NewGuid().ToString();
			SubscriptionPath = string.Format("/subscriptions/{0}/{1}", TestStream.Substring(9), GroupName);
			Response = await MakeJsonPut(SubscriptionPath,
				new {
					ResolveLinkTos = true,
					BufferSize = 10,
					ReadBatchSize = 11
				},
				_admin);
		}

		[OneTimeTearDown]
		public void TearDown() {
			Response?.Dispose();
		}

		[Test]
		public void returns_bad_request() {
			Assert.AreEqual(HttpStatusCode.BadRequest, Response.StatusCode);
		}
	}
}
