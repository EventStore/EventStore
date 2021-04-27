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
using System.Net.Http;
using System.Threading.Tasks;
using HttpStatusCode = System.Net.HttpStatusCode;
using EventStore.Transport.Http;

// ReSharper disable InconsistentNaming

namespace EventStore.Core.Tests.Http.PersistentSubscription {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	class when_nacking_a_message<TLogFormat, TStreamId> : with_subscription_having_events<TLogFormat, TStreamId> {
		private HttpResponseMessage _response;
		private string _nackLink;

		protected override async Task Given() {
			await base.Given();
			var json = await GetJson<JObject>(
				SubscriptionPath + "/1",
				ContentType.CompetingJson,
				_admin);
			Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
			_nackLink = json["entries"].Children().First()["links"].Children()
				.First(x => x.Value<string>("relation") == "nack").Value<string>("uri");
		}

		protected override async Task When() {
			_response = await MakePost(_nackLink, _admin);
		}

		[Test]
		public void returns_accepted() {
			Assert.AreEqual(HttpStatusCode.Accepted, _response.StatusCode);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	class when_nacking_messages<TLogFormat, TStreamId> : with_subscription_having_events<TLogFormat, TStreamId> {
		private HttpResponseMessage _response;
		private string _nackAllLink;

		protected override async Task Given() {
			await base.Given();
			var json = await GetJson<JObject>(
				SubscriptionPath + "/" + Events.Count,
				ContentType.CompetingJson,
				_admin);
			Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
			_nackAllLink = json["links"].Children().First(x => x.Value<string>("relation") == "nackAll")
				.Value<string>("uri");
		}

		protected override async Task When() {
			_response = await MakePost(_nackAllLink, _admin);
		}

		[Test]
		public void returns_accepted() {
			Assert.AreEqual(HttpStatusCode.Accepted, _response.StatusCode);
		}
	}
}
