// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using NUnit.Framework;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HttpStatusCode = System.Net.HttpStatusCode;
using EventStore.Transport.Http;

// ReSharper disable InconsistentNaming

namespace EventStore.Core.Tests.Http.PersistentSubscription {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
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
			Assert.DoesNotThrow(() => {
				_nackLink = json["entries"].Children().First()["links"].Children()
					.First(x => x.Value<string>("relation") == "nack").Value<string>("uri");
			});
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
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
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
			Assert.DoesNotThrow(() => {
				_nackAllLink = json["links"].Children().First(x => x.Value<string>("relation") == "nackAll")
					.Value<string>("uri");
			});
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
