using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Data;
using EventStore.Core.Tests.Integration;
using NUnit.Framework;

namespace EventStore.Core.Tests.Http.Cluster {
	[TestFixture]
	[Category("LongRunning")]
	public class when_requesting_from_slave : specification_with_cluster {
		private const string TestStream = "test-stream";
		private IPEndPoint _slaveEndPoint;
		private IPEndPoint _masterEndPoint;
		private HttpClient _client;

		protected override async Task Given() {
			var master = GetMaster();
			_masterEndPoint = master.ExternalHttpEndPoint;
			
			// Wait for the admin user to be created before starting our tests
			await master.AdminUserCreated;
			var replica = GetSlaves().First();
			_slaveEndPoint = replica.ExternalHttpEndPoint;
			_client = replica.CreateHttpClient();

			var path = $"streams/{TestStream}";
			var response = await PostEvent(_slaveEndPoint, path, requireMaster: false);
			Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
			var masterIndex = GetMaster().Db.Config.IndexCheckpoint.Read();
			AssertEx.IsOrBecomesTrue(()=> replica.Db.Config.IndexCheckpoint.Read() == masterIndex);
		}

		public override Task TestFixtureTearDown() {
			_client?.Dispose();
			return base.TestFixtureTearDown();
		}

		[Test]
		public async Task post_events_should_succeed_when_master_not_required() {
			var path = $"streams/{TestStream}";
			var response = await PostEvent(_slaveEndPoint, path, requireMaster: false);

			Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
		}

		[Test]
		public async Task delete_stream_should_succeed_when_master_not_required() {
			var path = $"streams/{TestStream}";
			var response = await DeleteStream(_slaveEndPoint, path, requireMaster: false);

			Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
		}

		[Test]
		public async Task read_from_stream_forward_should_succeed_when_master_not_required() {
			var path = $"streams/{TestStream}/0/forward/1";
			var response = await ReadStream(_slaveEndPoint, path, requireMaster: false);

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
		}

		[Test]
		public async Task read_from_stream_backward_should_succeed_when_master_not_required() {
			var path = $"streams/{TestStream}";
			var response = await ReadStream(_slaveEndPoint, path, requireMaster: false);

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
		}

		[Test]
		public async Task read_from_all_forward_should_succeed_when_master_not_required() {
			var path = $"streams/$all/00000000000000000000000000000000/forward/1";
			var response = await ReadStream(_slaveEndPoint, path, requireMaster: false);

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
		}

		[Test]
		public async Task read_from_all_backward_should_succeed_when_master_not_required() {
			var path = $"streams/$all";
			var response = await ReadStream(_slaveEndPoint, path, requireMaster: false);

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
		}

		[Test]
		public async Task should_redirect_to_master_when_writing_with_requires_master() {
			var path = $"streams/{TestStream}";
			var response = await PostEvent(_slaveEndPoint, path);

			Assert.AreEqual(HttpStatusCode.TemporaryRedirect, response.StatusCode);
			var masterLocation = CreateUri(_masterEndPoint, path);
			Assert.AreEqual(masterLocation, response.Headers.Location);
		}

		[Test]
		public async Task should_redirect_to_master_when_deleting_with_requires_master() {
			var path = $"streams/{TestStream}";
			var response = await DeleteStream(_slaveEndPoint, path, requireMaster: true);

			Assert.AreEqual(HttpStatusCode.TemporaryRedirect, response.StatusCode);
			var masterLocation = CreateUri(_masterEndPoint, path);
			Assert.AreEqual(masterLocation, response.Headers.Location);
		}

		[Test]
		public async Task should_redirect_to_master_when_reading_from_stream_backwards_with_requires_master() {
			var path = $"streams/{TestStream}";
			var response = await ReadStream(_slaveEndPoint, path, requireMaster: true);

			Assert.AreEqual(HttpStatusCode.TemporaryRedirect, response.StatusCode);
			var masterLocation = CreateUri(_masterEndPoint, path);
			Assert.AreEqual(masterLocation, response.Headers.Location);
		}

		[Test]
		public async Task should_redirect_to_master_when_reading_from_stream_forwards_with_requires_master() {
			var path = $"streams/{TestStream}/0/forward/1";
			var response = await ReadStream(_slaveEndPoint, path, requireMaster: true);

			Assert.AreEqual(HttpStatusCode.TemporaryRedirect, response.StatusCode);
			var masterLocation = CreateUri(_masterEndPoint, path);
			Assert.AreEqual(masterLocation, response.Headers.Location);
		}

		[Test]
		public async Task should_redirect_to_master_when_reading_from_all_backwards_with_requires_master() {
			var path = $"streams/$all";
			var response = await ReadStream(_slaveEndPoint, path, requireMaster: true);

			Assert.AreEqual(HttpStatusCode.TemporaryRedirect, response.StatusCode);
			var masterLocation = CreateUri(_masterEndPoint, path);
			Assert.AreEqual(masterLocation, response.Headers.Location);
		}

		[Test]
		public async Task should_redirect_to_master_when_reading_from_all_forwards_with_requires_master() {
			var path = $"streams/$all/00000000000000000000000000000000/forward/1";
			var response = await ReadStream(_slaveEndPoint, path, requireMaster: true);

			Assert.AreEqual(HttpStatusCode.TemporaryRedirect, response.StatusCode);
			var masterLocation = CreateUri(_masterEndPoint, path);
			Assert.AreEqual(masterLocation, response.Headers.Location);
		}

		private Task<HttpResponseMessage> ReadStream(IPEndPoint nodeEndpoint, string path, bool requireMaster) {
			var uri = CreateUri(nodeEndpoint, path);
			var request = CreateRequest(uri, HttpMethod.Get, requireMaster);
			request.Headers.Add("Accept", "application/json");
			return GetRequestResponse(request);
		}

		private Task<HttpResponseMessage> DeleteStream(IPEndPoint nodeEndpoint, string path, bool requireMaster = true) {
			var uri = CreateUri(nodeEndpoint, path);
			var request = CreateRequest(uri, HttpMethod.Delete, requireMaster);
			return GetRequestResponse(request);
		}

		private Task<HttpResponseMessage> PostEvent(IPEndPoint nodeEndpoint, string path, bool requireMaster = true) {
			var uri = CreateUri(nodeEndpoint, path);
			var request = CreateRequest(uri, HttpMethod.Post, requireMaster);

			request.Headers.Add("ES-EventType", "SomeType");
			request.Headers.Add("ES-ExpectedVersion", ExpectedVersion.Any.ToString());
			request.Headers.Add("ES-EventId", Guid.NewGuid().ToString());
			var data = "{a : \"1\", b:\"3\", c:\"5\" }";
			request.Content = new StringContent(data, Encoding.UTF8, "application/json");

			return GetRequestResponse(request);
		}
		
		private static string GetAuthorizationHeader(NetworkCredential credentials)
			=> Convert.ToBase64String(Encoding.ASCII.GetBytes($"{credentials.UserName}:{credentials.Password}"));

		private HttpRequestMessage CreateRequest(Uri uri, HttpMethod method, bool requireMaster) {
			var request = new HttpRequestMessage(method, uri);
			request.Headers.Add("ES-RequireMaster", requireMaster ? "True" : "False");
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
