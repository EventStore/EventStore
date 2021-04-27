using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using EventStore.Core.Data;
using EventStore.Core.Tests.Integration;
using NUnit.Framework;

namespace EventStore.Core.Tests.Http.Cluster {
	[Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class when_requesting_from_follower<TLogFormat, TStreamId> : specification_with_cluster<TLogFormat, TStreamId> {
		private const string TestStream = "test-stream";
		private IPEndPoint _followerEndPoint;
		private IPEndPoint _leaderEndPoint;
		private HttpClient _client;

		protected override async Task Given() {
			var leader = GetLeader();
			_leaderEndPoint = leader.HttpEndPoint;
			
			// Wait for the admin user to be created
			await leader.AdminUserCreated;
			var replica = GetFollowers().First();
			_followerEndPoint = replica.HttpEndPoint;
			_client = replica.CreateHttpClient();

			// Wait for the admin user created event to be replicated before starting our tests
			var leaderIndex = GetLeader().Db.Config.IndexCheckpoint.Read();
			AssertEx.IsOrBecomesTrue(()=> replica.Db.Config.IndexCheckpoint.Read() == leaderIndex);
			
			var path = $"streams/{TestStream}";
			var response = await PostEvent(_followerEndPoint, path, requireLeader: false);
			Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
			leaderIndex = GetLeader().Db.Config.IndexCheckpoint.Read();
			AssertEx.IsOrBecomesTrue(()=> replica.Db.Config.IndexCheckpoint.Read() == leaderIndex);
		}

		public override Task TestFixtureTearDown() {
			_client?.Dispose();
			return base.TestFixtureTearDown();
		}

		[Test]
		public async Task post_events_should_succeed_when_leader_not_required() {
			var path = $"streams/{TestStream}";
			var response = await PostEvent(_followerEndPoint, path, requireLeader: false);

			Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
		}

		[Test]
		public async Task delete_stream_should_succeed_when_leader_not_required() {
			var path = $"streams/{TestStream}";
			var response = await DeleteStream(_followerEndPoint, path, requireLeader: false);

			Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
		}

		[Test]
		public async Task read_from_stream_forward_should_succeed_when_leader_not_required() {
			var path = $"streams/{TestStream}/0/forward/1";
			var response = await ReadStream(_followerEndPoint, path, requireLeader: false);

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
		}

		[Test]
		public async Task read_from_stream_backward_should_succeed_when_leader_not_required() {
			var path = $"streams/{TestStream}";
			var response = await ReadStream(_followerEndPoint, path, requireLeader: false);

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
		}

		[Test]
		public async Task read_from_all_forward_should_succeed_when_leader_not_required() {
			var path = $"streams/$all/00000000000000000000000000000000/forward/1";
			var response = await ReadStream(_followerEndPoint, path, requireLeader: false);

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
		}

		[Test]
		public async Task read_from_all_backward_should_succeed_when_leader_not_required() {
			var path = $"streams/$all";
			var response = await ReadStream(_followerEndPoint, path, requireLeader: false);

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
		}

		[Test]
		public async Task should_redirect_to_leader_when_writing_with_requires_leader() {
			var path = $"streams/{TestStream}";
			var response = await PostEvent(_followerEndPoint, path);

			Assert.AreEqual(HttpStatusCode.TemporaryRedirect, response.StatusCode);
			var leaderLocation = CreateUri(_leaderEndPoint, path);
			Assert.AreEqual(leaderLocation, response.Headers.Location);
		}

		[Test]
		public async Task should_redirect_to_leader_when_deleting_with_requires_leader() {
			var path = $"streams/{TestStream}";
			var response = await DeleteStream(_followerEndPoint, path, requireLeader: true);

			Assert.AreEqual(HttpStatusCode.TemporaryRedirect, response.StatusCode);
			var leaderLocation = CreateUri(_leaderEndPoint, path);
			Assert.AreEqual(leaderLocation, response.Headers.Location);
		}

		[Test]
		public async Task should_redirect_to_leader_when_reading_from_stream_backwards_with_requires_leader() {
			var path = $"streams/{TestStream}";
			var response = await ReadStream(_followerEndPoint, path, requireLeader: true);

			Assert.AreEqual(HttpStatusCode.TemporaryRedirect, response.StatusCode);
			var leaderLocation = CreateUri(_leaderEndPoint, path);
			Assert.AreEqual(leaderLocation, response.Headers.Location);
		}

		[Test]
		public async Task should_redirect_to_leader_when_reading_from_stream_forwards_with_requires_leader() {
			var path = $"streams/{TestStream}/0/forward/1";
			var response = await ReadStream(_followerEndPoint, path, requireLeader: true);

			Assert.AreEqual(HttpStatusCode.TemporaryRedirect, response.StatusCode);
			var leaderLocation = CreateUri(_leaderEndPoint, path);
			Assert.AreEqual(leaderLocation, response.Headers.Location);
		}

		[Test]
		public async Task should_redirect_to_leader_when_reading_from_all_backwards_with_requires_leader() {
			var path = $"streams/$all";
			var response = await ReadStream(_followerEndPoint, path, requireLeader: true);

			Assert.AreEqual(HttpStatusCode.TemporaryRedirect, response.StatusCode);
			var leaderLocation = CreateUri(_leaderEndPoint, path);
			Assert.AreEqual(leaderLocation, response.Headers.Location);
		}

		[Test]
		public async Task should_redirect_to_leader_when_reading_from_all_forwards_with_requires_leader() {
			var path = $"streams/$all/00000000000000000000000000000000/forward/1";
			var response = await ReadStream(_followerEndPoint, path, requireLeader: true);

			Assert.AreEqual(HttpStatusCode.TemporaryRedirect, response.StatusCode);
			var leaderLocation = CreateUri(_leaderEndPoint, path);
			Assert.AreEqual(leaderLocation, response.Headers.Location);
		}

		private Task<HttpResponseMessage> ReadStream(IPEndPoint nodeEndpoint, string path, bool requireLeader) {
			var uri = CreateUri(nodeEndpoint, path);
			var request = CreateRequest(uri, HttpMethod.Get, requireLeader);
			request.Headers.Add("Accept", "application/json");
			return GetRequestResponse(request);
		}

		private Task<HttpResponseMessage> DeleteStream(IPEndPoint nodeEndpoint, string path, bool requireLeader = true) {
			var uri = CreateUri(nodeEndpoint, path);
			var request = CreateRequest(uri, HttpMethod.Delete, requireLeader);
			return GetRequestResponse(request);
		}

		private Task<HttpResponseMessage> PostEvent(IPEndPoint nodeEndpoint, string path, bool requireLeader = true) {
			var uri = CreateUri(nodeEndpoint, path);
			var request = CreateRequest(uri, HttpMethod.Post, requireLeader);

			request.Headers.Add("ES-EventType", "SomeType");
			request.Headers.Add("ES-ExpectedVersion", ExpectedVersion.Any.ToString());
			request.Headers.Add("ES-EventId", Guid.NewGuid().ToString());
			var data = "{a : \"1\", b:\"3\", c:\"5\" }";
			request.Content = new StringContent(data, Encoding.UTF8, "application/json");

			return GetRequestResponse(request);
		}
		
		private static string GetAuthorizationHeader(NetworkCredential credentials)
			=> Convert.ToBase64String(Encoding.ASCII.GetBytes($"{credentials.UserName}:{credentials.Password}"));

		private HttpRequestMessage CreateRequest(Uri uri, HttpMethod method, bool requireLeader) {
			var request = new HttpRequestMessage(method, uri);
			request.Headers.Add("ES-RequireLeader", requireLeader ? "True" : "False");
			request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            					GetAuthorizationHeader(DefaultData.AdminNetworkCredentials));
			return request;
		}

		private async Task<HttpResponseMessage> GetRequestResponse(HttpRequestMessage request) {
			var response = await _client.SendAsync(request);
			return response;
		}

		private Uri CreateUri(IPEndPoint nodeEndpoint, string path) {
			var uriBuilder = new UriBuilder("https", nodeEndpoint.Address.ToString(), nodeEndpoint.Port, path);
			return uriBuilder.Uri;
		}
	}
}
