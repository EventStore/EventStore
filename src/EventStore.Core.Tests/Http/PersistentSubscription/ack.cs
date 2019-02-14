using System;
using System.Net;
using System.Text.RegularExpressions;
using EventStore.Core.Tests.Http.Users.users;
using NUnit.Framework;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using System.Linq;
using HttpStatusCode = System.Net.HttpStatusCode;
using EventStore.Transport.Http;

// ReSharper disable InconsistentNaming

namespace EventStore.Core.Tests.Http.PersistentSubscription {
	class when_acking_a_message : with_subscription_having_events {
		private HttpWebResponse _response;
		private string _ackLink;

		protected override void Given() {
			base.Given();
			var json = GetJson<JObject>(
				SubscriptionPath + "/1",
				ContentType.CompetingJson,
				_admin);
			Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
			_ackLink = ((JObject)json)["entries"].Children().First()["links"].Children()
				.First(x => x.Value<string>("relation") == "ack").Value<string>("uri");
		}

		[TearDown]
		public void TearDown() {
			_response.Close();
		}

		protected override void When() {
			_response = MakePost(_ackLink, _admin);
		}

		[Test]
		public void returns_accepted() {
			Assert.AreEqual(HttpStatusCode.Accepted, _response.StatusCode);
		}
	}

	class when_acking_messages : with_subscription_having_events {
		private HttpWebResponse _response;
		private string _ackAllLink;

		protected override void Given() {
			base.Given();
			var json = GetJson<JObject>(
				SubscriptionPath + "/" + Events.Count,
				ContentType.CompetingJson,
				_admin);
			Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
			_ackAllLink = ((JObject)json)["links"].Children().First(x => x.Value<string>("relation") == "ackAll")
				.Value<string>("uri");
		}

		[TearDown]
		public void TearDown() {
			_response.Close();
		}

		protected override void When() {
			_response = MakePost(_ackAllLink, _admin);
		}

		[Test]
		public void returns_accepted() {
			Assert.AreEqual(HttpStatusCode.Accepted, _response.StatusCode);
		}
	}
}
