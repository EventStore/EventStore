using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
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

		protected override void Given() {
			var master = GetMaster();
			if (!master.AdminUserCreatedEvent.WaitOne(TimeSpan.FromSeconds(5)))
				Assert.Fail("Timed out waiting for admin user to be created.");
			_slaveEndPoint = GetSlaves().First().ExternalHttpEndPoint;
			_masterEndPoint = master.ExternalHttpEndPoint;
		}

		[Test]
		public void post_events_should_succeed_when_master_not_required() {
			var path = $"streams/{TestStream}";
			var response = PostEvent(_slaveEndPoint, path, requireMaster: false);

			Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
		}

		[Test]
		public void delete_stream_should_succeed_when_master_not_required() {
			var path = $"streams/{TestStream}";
			var response = DeleteStream(_slaveEndPoint, path, requireMaster: false);

			Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
		}

		[Test]
		public void read_from_stream_forward_should_succeed_when_master_not_required() {
			var path = $"streams/{TestStream}/0/forward/1";
			var response = ReadStream(_slaveEndPoint, path, requireMaster: false);

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
		}

		[Test]
		public void read_from_stream_backward_should_succeed_when_master_not_required() {
			var path = $"streams/{TestStream}";
			var response = ReadStream(_slaveEndPoint, path, requireMaster: false);

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
		}

		[Test]
		public void read_from_all_forward_should_succeed_when_master_not_required() {
			var path = $"streams/$all/00000000000000000000000000000000/forward/1";
			var response = ReadStream(_slaveEndPoint, path, requireMaster: false);

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
		}

		[Test]
		public void read_from_all_backward_should_succeed_when_master_not_required() {
			var path = $"streams/$all";
			var response = ReadStream(_slaveEndPoint, path, requireMaster: false);

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
		}

		[Test]
		public void should_redirect_to_master_when_writing_with_requires_master() {
			var path = $"streams/{TestStream}";
			var response = PostEvent(_slaveEndPoint, path);

			Assert.AreEqual(HttpStatusCode.TemporaryRedirect, response.StatusCode);
			var masterLocation = CreateUri(_masterEndPoint, path);
			Assert.AreEqual(masterLocation.ToString(), response.Headers["Location"]);
		}

		[Test]
		public void should_redirect_to_master_when_deleting_with_requires_master() {
			var path = $"streams/{TestStream}";
			var response = DeleteStream(_slaveEndPoint, path, requireMaster: true);

			Assert.AreEqual(HttpStatusCode.TemporaryRedirect, response.StatusCode);
			var masterLocation = CreateUri(_masterEndPoint, path);
			Assert.AreEqual(masterLocation.ToString(), response.Headers["Location"]);
		}

		[Test]
		public void should_redirect_to_master_when_reading_from_stream_backwards_with_requires_master() {
			var path = $"streams/{TestStream}";
			var response = ReadStream(_slaveEndPoint, path, requireMaster: true);

			Assert.AreEqual(HttpStatusCode.TemporaryRedirect, response.StatusCode);
			var masterLocation = CreateUri(_masterEndPoint, path);
			Assert.AreEqual(masterLocation.ToString(), response.Headers["Location"]);
		}

		[Test]
		public void should_redirect_to_master_when_reading_from_stream_forwards_with_requires_master() {
			var path = $"streams/{TestStream}/0/forward/1";
			var response = ReadStream(_slaveEndPoint, path, requireMaster: true);

			Assert.AreEqual(HttpStatusCode.TemporaryRedirect, response.StatusCode);
			var masterLocation = CreateUri(_masterEndPoint, path);
			Assert.AreEqual(masterLocation.ToString(), response.Headers["Location"]);
		}

		[Test]
		public void should_redirect_to_master_when_reading_from_all_backwards_with_requires_master() {
			var path = $"streams/$all";
			var response = ReadStream(_slaveEndPoint, path, requireMaster: true);

			Assert.AreEqual(HttpStatusCode.TemporaryRedirect, response.StatusCode);
			var masterLocation = CreateUri(_masterEndPoint, path);
			Assert.AreEqual(masterLocation.ToString(), response.Headers["Location"]);
		}

		[Test]
		public void should_redirect_to_master_when_reading_from_all_forwards_with_requires_master() {
			var path = $"streams/$all/00000000000000000000000000000000/forward/1";
			var response = ReadStream(_slaveEndPoint, path, requireMaster: true);

			Assert.AreEqual(HttpStatusCode.TemporaryRedirect, response.StatusCode);
			var masterLocation = CreateUri(_masterEndPoint, path);
			Assert.AreEqual(masterLocation.ToString(), response.Headers["Location"]);
		}

		private HttpWebResponse ReadStream(IPEndPoint nodeEndpoint, string path, bool requireMaster) {
			var uri = CreateUri(nodeEndpoint, path);
			var request = CreateRequest(uri, "GET", requireMaster);
			request.Accept = "application/json";
			return GetRequestResponse(request);
		}

		private HttpWebResponse DeleteStream(IPEndPoint nodeEndpoint, string path, bool requireMaster = true) {
			var uri = CreateUri(nodeEndpoint, path);
			var request = CreateRequest(uri, "DELETE", requireMaster);
			return GetRequestResponse(request);
		}

		private HttpWebResponse PostEvent(IPEndPoint nodeEndpoint, string path, bool requireMaster = true) {
			var uri = CreateUri(nodeEndpoint, path);
			var request = CreateRequest(uri, "POST", requireMaster);

			request.Headers.Add("ES-EventType", "SomeType");
			request.Headers.Add("ES-ExpectedVersion", ExpectedVersion.Any.ToString());
			request.Headers.Add("ES-EventId", Guid.NewGuid().ToString());

			var data = Encoding.UTF8.GetBytes("{a : \"1\", b:\"3\", c:\"5\" }");
			request.ContentLength = data.Length;
			request.ContentType = "application/json";
			request.GetRequestStream().Write(data, 0, data.Length);

			return GetRequestResponse(request);
		}

		private HttpWebRequest CreateRequest(Uri uri, string method, bool requireMaster) {
			var request = (HttpWebRequest)WebRequest.Create(uri);
			request.Method = method;
			request.AllowAutoRedirect = false;
			request.Headers.Add("ES-RequireMaster", requireMaster ? "True" : "False");

			request.UseDefaultCredentials = false;
			request.Credentials = DefaultData.AdminNetworkCredentials;
			request.PreAuthenticate = true;
			return request;
		}

		private HttpWebResponse GetRequestResponse(HttpWebRequest request) {
			HttpWebResponse response;
			try {
				response = (HttpWebResponse)request.GetResponse();
			} catch (WebException ex) {
				response = (HttpWebResponse)ex.Response;
			}
			return response;
		}

		private Uri CreateUri(IPEndPoint nodeEndpoint, string path) {
			var uriBuilder = new UriBuilder("http", nodeEndpoint.Address.ToString(), nodeEndpoint.Port, path);
			return uriBuilder.Uri;
		}
	}
}
