using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.Transport.Http;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using EventStore.Core.Services;
using HttpStatusCode = System.Net.HttpStatusCode;
using EventStore.Core.Tests.Http.Users.users;
using EventStore.Core.TransactionLog.Services;
using Microsoft.Extensions.Primitives;

namespace EventStore.Core.Tests.Http.Streams {
	namespace append_to_stream {
		public abstract class ExpectedVersionSpecification : with_admin_user {
			public string WrongExpectedVersionDesc {
				get { return "Wrong expected EventNumber"; }
			}

			public string DeletedStreamDesc {
				get { return "Stream deleted"; }
			}

			public Task<HttpResponseMessage> PostEventWithExpectedVersion(long expectedVersion) {
				var request = CreateRequest(TestStream, "", "POST", "application/json");
				request.Headers.Add("ES-EventType", "SomeType");
				request.Headers.Add("ES-ExpectedVersion", expectedVersion.ToString());
				request.Headers.Add("ES-EventId", Guid.NewGuid().ToString());
				var data = Encoding.UTF8.GetBytes("{a : \"1\", b:\"3\", c:\"5\" }");
				request.Content = new ByteArrayContent(data) {
					Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
				};
				return GetRequestResponse(request);
			}

			public Task HardDeleteTestStream() {
				return DeleteTestStream(true);
			}

			public async Task DeleteTestStream(bool hardDelete = false) {
				var deleteRequest = CreateRequest(TestStream, "", "DELETE", "application/json");
				deleteRequest.Headers.Add("ES-ExpectedVersion", (ExpectedVersion.Any).ToString());
				deleteRequest.Headers.Add("ES-HardDelete", hardDelete.ToString());
				await GetRequestResponse(deleteRequest);
			}

			public async Task<List<JToken>> GetTestStream() {
				var stream = await GetJson<JObject>(TestStream + "/0/forward/10", accept: ContentType.Json);
				return stream != null ? stream["entries"].ToList() : new List<JToken>();
			}
		}

		[TestFixture, Category("LongRunning")]
		public class
			should_create_stream_with_no_stream_exp_ver_on_first_write_if_does_not_exist :
				ExpectedVersionSpecification {
			private HttpResponseMessage _response;
			private List<JToken> _read;

			protected override Task Given() => Task.CompletedTask;

			protected override async Task When() {
				_response = await PostEventWithExpectedVersion(ExpectedVersion.NoStream);
				_read = await GetTestStream();
			}

			[Test]
			public void should_return_created_status_code() {
				Assert.AreEqual(HttpStatusCode.Created, _response.StatusCode);
			}

			[Test]
			public void should_create_stream() {
				Assert.AreEqual(1, _read.Count);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class should_fail_appending_with_no_stream_exp_ver_to_existing_stream : ExpectedVersionSpecification {
			private HttpResponseMessage _response;
			private List<JToken> _read;

			protected override Task Given() {
				return PostEventWithExpectedVersion(ExpectedVersion.Any);
			}

			protected override async Task When() {
				_response = await PostEventWithExpectedVersion(ExpectedVersion.NoStream);
				_read = await GetTestStream();
			}

			[Test]
			public void should_return_bad_request_with_wrong_expected_version() {
				Assert.AreEqual(HttpStatusCode.BadRequest, _response.StatusCode);
				// Assert.AreEqual(WrongExpectedVersionDesc, _response.StatusDescription);
			}

			[Test]
			public void should_return_the_current_version_in_the_header() {
				Assert.AreEqual("0",
					new StringValues(_response.Headers.GetValues(SystemHeaders.CurrentVersion).ToArray()));
			}

			[Test]
			public void should_not_append_to_stream() {
				Assert.AreEqual(1, _read.Count);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class
			should_create_stream_with_any_exp_ver_on_first_write_if_does_not_exist : ExpectedVersionSpecification {
			private HttpResponseMessage _response;
			private List<JToken> _read;

			protected override Task Given() => Task.CompletedTask;

			protected override async Task When() {
				_response = await PostEventWithExpectedVersion(ExpectedVersion.Any);
				_read = await GetTestStream();
			}

			[Test]
			public void should_return_created_status_code() {
				Assert.AreEqual(HttpStatusCode.Created, _response.StatusCode);
			}

			[Test]
			public void should_create_stream() {
				Assert.AreEqual(1, _read.Count);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class should_append_with_any_exp_ver_to_existing_stream : ExpectedVersionSpecification {
			private HttpResponseMessage _response;
			private List<JToken> _read;

			protected override async Task Given() {
				for (var i = 0; i < 3; i++) {
					await PostEventWithExpectedVersion(ExpectedVersion.Any);
				}
			}

			protected override async Task When() {
				_response = await PostEventWithExpectedVersion(ExpectedVersion.Any);
				_read = await GetTestStream();
			}

			[Test]
			public void should_return_created_status_code() {
				Assert.AreEqual(HttpStatusCode.Created, _response.StatusCode);
			}

			[Test]
			public void should_append_event_to_stream() {
				Assert.AreEqual(4, _read.Count);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class should_fail_appending_with_any_exp_ver_to_deleted_stream : ExpectedVersionSpecification {
			private HttpResponseMessage _response;

			protected override Task Given() {
				return HardDeleteTestStream();
			}

			protected override async Task When() {
				_response = await PostEventWithExpectedVersion(ExpectedVersion.Any);
			}

			[Test]
			public void should_return_gone_status_code() {
				Assert.AreEqual(HttpStatusCode.Gone, _response.StatusCode);
				// Assert.AreEqual(DeletedStreamDesc, _response.StatusDescription);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class should_append_with_correct_exp_ver_to_existing_stream : ExpectedVersionSpecification {
			private HttpResponseMessage _response;
			private List<JToken> _read;

			protected override async Task Given() {
				for (var i = 0; i < 3; i++) {
					await PostEventWithExpectedVersion(ExpectedVersion.Any);
				}
			}

			protected override async Task When() {
				_response = await PostEventWithExpectedVersion(2);
				_read = await GetTestStream();
			}

			[Test]
			public void should_return_created_status_code() {
				Assert.AreEqual(HttpStatusCode.Created, _response.StatusCode);
			}

			[Test]
			public void should_append_event_to_stream() {
				Assert.AreEqual(4, _read.Count);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class should_fail_appending_with_wrong_exp_ver_to_existing_stream : ExpectedVersionSpecification {
			private HttpResponseMessage _response;
			private List<JToken> _read;

			protected override async Task Given() {
				for (var i = 0; i < 2; i++) {
					await PostEventWithExpectedVersion(ExpectedVersion.Any);
				}
			}

			protected override async Task When() {
				_response = await PostEventWithExpectedVersion(4);
				_read = await GetTestStream();
			}

			[Test]
			public void should_return_bad_request_with_wrong_expected_version() {
				Assert.AreEqual(HttpStatusCode.BadRequest, _response.StatusCode);
				//// Assert.AreEqual(WrongExpectedVersionDesc, _response.StatusDescription);
			}

			[Test]
			public void should_return_the_current_version_in_the_header() {
				Assert.AreEqual("1",
					new StringValues(_response.Headers.GetValues(SystemHeaders.CurrentVersion).ToArray()));
			}

			[Test]
			public void should_not_append_event() {
				Assert.AreEqual(2, _read.Count);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class should_fail_appending_with_wrong_exp_ver_to_new_stream : ExpectedVersionSpecification {
			private HttpResponseMessage _response;
			private List<JToken> _read;

			protected override Task Given() => Task.CompletedTask;

			protected override async Task When() {
				_response = await PostEventWithExpectedVersion(1);
				_read = await GetTestStream();
			}

			[Test]
			public void should_return_bad_request_with_wrong_expected_version() {
				Assert.AreEqual(HttpStatusCode.BadRequest, _response.StatusCode);
				// Assert.AreEqual(WrongExpectedVersionDesc, _response.StatusDescription);
			}

			[Test]
			public void should_return_the_current_version_in_the_header() {
				Assert.AreEqual("-1",
					new StringValues(_response.Headers.GetValues(SystemHeaders.CurrentVersion).ToArray()));
			}

			[Test]
			public void should_not_append_to_stream() {
				Assert.AreEqual(0, _read.Count);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class should_fail_appending_with_correct_exp_ver_to_deleted_stream : ExpectedVersionSpecification {
			private HttpResponseMessage _response;

			protected override Task Given() {
				return HardDeleteTestStream();
			}

			protected override async Task When() {
				_response = await PostEventWithExpectedVersion(ExpectedVersion.NoStream);
			}

			[Test]
			public void should_return_gone_status_code() {
				Assert.AreEqual(HttpStatusCode.Gone, _response.StatusCode);
				// Assert.AreEqual(DeletedStreamDesc, _response.StatusDescription);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class should_fail_appending_with_invalid_exp_ver_to_deleted_stream : ExpectedVersionSpecification {
			private HttpResponseMessage _response;

			protected override Task Given() {
				return HardDeleteTestStream();
			}

			protected override async Task When() {
				_response = await PostEventWithExpectedVersion(5);
			}

			[Test]
			public void should_return_gone_status_code() {
				Assert.AreEqual(HttpStatusCode.Gone, _response.StatusCode);
				// Assert.AreEqual(DeletedStreamDesc, _response.StatusDescription);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class should_append_with_stream_exists_exp_ver_to_existing_stream : ExpectedVersionSpecification {
			private HttpResponseMessage _response;
			private List<JToken> _read;

			protected override Task Given() {
				return PostEventWithExpectedVersion(ExpectedVersion.Any);
			}

			protected override async Task When() {
				_response = await PostEventWithExpectedVersion(ExpectedVersion.StreamExists);
				_read = await GetTestStream();
			}

			[Test]
			public void should_return_created_status_code() {
				Assert.AreEqual(HttpStatusCode.Created, _response.StatusCode);
			}

			[Test]
			public void should_append_event_to_stream() {
				Assert.AreEqual(2, _read.Count);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class
			should_append_with_stream_exists_exp_ver_to_stream_with_multiple_events : ExpectedVersionSpecification {
			private HttpResponseMessage _response;
			private List<JToken> _read;

			protected override async Task Given() {
				for (var i = 0; i < 2; i++) {
					await PostEventWithExpectedVersion(ExpectedVersion.Any);
				}
			}

			protected override async Task When() {
				_response = await PostEventWithExpectedVersion(ExpectedVersion.StreamExists);
				_read = await GetTestStream();
			}

			[Test]
			public void should_return_created_status_code() {
				Assert.AreEqual(HttpStatusCode.Created, _response.StatusCode);
			}

			[Test]
			public void should_append_event_to_stream() {
				Assert.AreEqual(3, _read.Count);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class should_append_with_stream_exists_exp_ver_if_metadata_stream_exists : ExpectedVersionSpecification {
			private HttpResponseMessage _response;
			private List<JToken> _read;

			protected override async Task Given() {
				var request = CreateRequest(TestMetadataStream, "", "POST", "application/json");
				request.Headers.Add("ES-EventType", "$user-created");
				request.Headers.Add("ES-ExpectedVersion", ExpectedVersion.Any.ToString());
				request.Headers.Add("ES-EventId", Guid.NewGuid().ToString());
				var data = Encoding.UTF8.GetBytes("{a : \"1\", b:\"3\", c:\"5\" }");
				request.Content = new ByteArrayContent(data) {
					Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
				};
				await GetRequestResponse(request);
			}

			protected override async Task When() {
				_response = await PostEventWithExpectedVersion(ExpectedVersion.StreamExists);
				_read = await GetTestStream();
			}

			[Test]
			public void should_return_created_status_code() {
				Assert.AreEqual(HttpStatusCode.Created, _response.StatusCode);
			}

			[Test]
			public void should_append_event_to_stream() {
				Assert.AreEqual(1, _read.Count);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class
			should_fail_appending_with_stream_exists_exp_ver_and_stream_does_not_exist : ExpectedVersionSpecification {
			private HttpResponseMessage _response;
			private List<JToken> _read;

			protected override Task Given() => Task.CompletedTask;

			protected override async Task When() {
				_response = await PostEventWithExpectedVersion(ExpectedVersion.StreamExists);
				_read = await GetTestStream();
			}

			[Test]
			public void should_return_bad_request_with_wrong_expected_version() {
				Assert.AreEqual(HttpStatusCode.BadRequest, _response.StatusCode);
				// Assert.AreEqual(WrongExpectedVersionDesc, _response.StatusDescription);
			}


			[Test]
			public void should_return_the_current_version_in_the_header() {
				Assert.AreEqual("-1",
					new StringValues(_response.Headers.GetValues(SystemHeaders.CurrentVersion).ToArray()));
			}

			[Test]
			public void should_not_append_event_to_stream() {
				Assert.AreEqual(0, _read.Count);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class
			should_fail_appending_with_stream_exists_exp_ver_to_hard_deleted_stream : ExpectedVersionSpecification {
			private HttpResponseMessage _response;

			protected override Task Given() {
				return HardDeleteTestStream();
			}

			protected override async Task When() {
				_response = await PostEventWithExpectedVersion(ExpectedVersion.StreamExists);
			}

			[Test]
			public void should_return_gone_status_code() {
				Assert.AreEqual(HttpStatusCode.Gone, _response.StatusCode);
				// Assert.AreEqual(DeletedStreamDesc, _response.StatusDescription);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class
			should_fail_appending_with_stream_exists_exp_ver_to_soft_deleted_stream : ExpectedVersionSpecification {
			private HttpResponseMessage _response;

			protected override Task Given() {
				return DeleteTestStream();
			}

			protected override async Task When() {
				_response = await PostEventWithExpectedVersion(ExpectedVersion.StreamExists);
			}

			[Test]
			public void should_return_gone_status_code() {
				Assert.AreEqual(HttpStatusCode.Gone, _response.StatusCode);
				// Assert.AreEqual(DeletedStreamDesc, _response.StatusDescription);
			}
		}
	}
}
