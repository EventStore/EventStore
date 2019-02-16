using System;
using System.Linq;
using System.Net;
using EventStore.ClientAPI;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;
using Newtonsoft.Json.Linq;

namespace EventStore.Core.Tests.Http.StreamSecurity {
	abstract class SpecificationWithUsers : HttpBehaviorSpecification {
		protected override void Given() {
			PostUser("user1", "User 1", "user1!", "other");
			PostUser("user2", "User 2", "user2!", "other");
			PostUser("guest", "Guest", "guest!");
		}

		protected readonly ICredentials _admin = DefaultData.AdminNetworkCredentials;

		protected override bool GivenSkipInitializeStandardUsersCheck() {
			return false;
		}

		protected override MiniNode CreateMiniNode() {
			return new MiniNode(PathName, skipInitializeStandardUsersCheck: GivenSkipInitializeStandardUsersCheck(),
				enableTrustedAuth: true);
		}

		protected void PostUser(string login, string userFullName, string password, params string[] groups) {
			var response = MakeJsonPost(
				"/users/", new {LoginName = login + Tag, FullName = userFullName, Groups = groups, Password = password},
				_admin);
			Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
		}

		protected string PostMetadata(StreamMetadata metadata) {
			var response = MakeArrayEventsPost(
				TestMetadataStream, new[] {new {EventId = Guid.NewGuid(), EventType = "event-type", Data = metadata}});
			Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
			return response.Headers[HttpResponseHeader.Location];
		}

		protected string PostEvent(int i) {
			var response = MakeArrayEventsPost(
				TestStream, new[] {new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {Number = i}}});
			Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
			return response.Headers[HttpResponseHeader.Location];
		}

		protected HttpWebResponse PostEvent<T>(T data, ICredentials credentials = null) {
			return MakeArrayEventsPost(
				TestStream, new[] {new {EventId = Guid.NewGuid(), EventType = "event-type", Data = data}}, credentials);
		}

		protected string GetLink(JObject feed, string relation) {
			var rel = (from JObject link in feed["links"]
				from JProperty attr in link
				where attr.Name == "relation" && (string)attr.Value == relation
				select link).SingleOrDefault();
			return (rel == null) ? null : (string)rel["uri"];
		}

		protected NetworkCredential GetCorrectCredentialsFor(string user) {
			return new NetworkCredential(user + Tag, user + "!");
		}
	}
}
