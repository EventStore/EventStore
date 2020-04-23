﻿using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.Tests.Http.Streams.basic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Xml.Linq;
using EventStore.Common.Utils;
using EventStore.Core.Tests.Http.Users.users;

namespace EventStore.Core.Tests.Http.Streams {
	[TestFixture]
	public class when_posting_metadata_as_json_to_non_existing_stream : with_admin_user {
		private HttpResponseMessage _response;

		protected override Task Given() => Task.CompletedTask;

		protected override async Task When() {
			var req = CreateRawJsonPostRequest(TestStream + "/metadata", "POST", new { A = "1" },
				DefaultData.AdminNetworkCredentials);
			req.Headers.Add("ES-EventId", Guid.NewGuid().ToString());
			_response = await _client.SendAsync(req);
		}

		[Test]
		public void returns_created_status_code() {
			Assert.AreEqual(HttpStatusCode.Created, _response.StatusCode);
		}

		[Test]
		public void returns_a_location_header() {
			Assert.IsNotEmpty(_response.Headers.GetLocationAsString());
		}

		[Test]
		public async Task returns_a_location_header_that_can_be_read_as_json() {
			var json = await GetJson<JObject>(_response.Headers.GetLocationAsString());
			HelperExtensions.AssertJson(new { A = "1" }, json);
		}
	}

	[TestFixture]
	public class when_posting_metadata_as_json_to_existing_stream : HttpBehaviorSpecificationWithSingleEvent {
		protected override async Task Given() {
			_response = await MakeArrayEventsPost(
				TestStream,
				new[] { new { EventId = Guid.NewGuid(), EventType = "event-type", Data = new { A = "1" } } });
		}

		protected override async Task When() {
			var req = CreateRawJsonPostRequest(TestStream + "/metadata", "POST", new { A = "1" },
				DefaultData.AdminNetworkCredentials);
			req.Headers.Add("ES-EventId", Guid.NewGuid().ToString());
			_response = await _client.SendAsync(req);
		}

		[Test]
		public void returns_created_status_code() {
			Assert.AreEqual(HttpStatusCode.Created, _response.StatusCode);
		}

		[Test]
		public void returns_a_location_header() {
			Assert.IsNotEmpty(_response.Headers.GetLocationAsString());
		}

		[Test]
		public async Task returns_a_location_header_that_can_be_read_as_json() {
			var json = await GetJson<JObject>(_response.Headers.GetLocationAsString());
			HelperExtensions.AssertJson(new { A = "1" }, json);
		}
	}

	[TestFixture]
	public class
		when_getting_metadata_for_an_existing_stream_without_an_accept_header :
			HttpBehaviorSpecificationWithSingleEvent {
		protected override Task When() {
			return Get(TestStream + "/metadata", null, null, DefaultData.AdminNetworkCredentials, false);
		}

		[Test]
		public void returns_ok_status_code() {
			Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
		}

		[Test]
		public void returns_empty_body() {
			Assert.AreEqual(Empty.Xml, _lastResponseBody);
		}
	}

	[TestFixture]
	public class
		when_getting_metadata_for_an_existing_stream_and_no_metadata_exists : HttpBehaviorSpecificationWithSingleEvent {
		protected override async Task Given() {
			_response = await MakeArrayEventsPost(
				TestStream,
				new[] { new { EventId = Guid.NewGuid(), EventType = "event-type", Data = new { A = "1" } } });
		}

		protected override Task When() {
			return Get(TestStream + "/metadata", String.Empty, Transport.Http.ContentType.Json,
				DefaultData.AdminNetworkCredentials);
		}

		[Test]
		public void returns_ok_status_code() {
			Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
		}

		[Test]
		public void returns_empty_etag() {
			Assert.Null(_lastResponse.Headers.ETag);
		}

		[Test]
		public void returns_empty_body() {
			Assert.AreEqual(Empty.Json, _lastResponseBody);
		}
	}
}
