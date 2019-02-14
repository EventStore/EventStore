using System;
using System.Net;
using System.Text;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.Tests.Http.Users;
using EventStore.Transport.Http;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using HttpStatusCode = System.Net.HttpStatusCode;
using System.Linq;
using System.Xml.Linq;
using System.IO;

namespace EventStore.Core.Tests.Http.Streams {
	namespace basic {
		[TestFixture, Category("LongRunning")]
		public class
			when_requesting_a_single_event_in_the_stream_as_atom_json : HttpBehaviorSpecificationWithSingleEvent {
			private JObject _json;

			protected override void When() {
				_json = GetJson<JObject>(TestStream + "/0", accept: ContentType.AtomJson);
			}

			[Test]
			public void request_succeeds() {
				Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
			}

			[Test]
			public void returns_correct_body() {
				HelperExtensions.AssertJson(new {Content = new {Data = new {A = "1"}}}, _json);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class
			when_requesting_a_single_event_in_the_stream_as_atom_xml : HttpBehaviorSpecificationWithSingleEvent {
			private XDocument document;

			protected override void When() {
				Get(TestStream + "/0", "", accept: ContentType.Atom);
				document = XDocument.Parse(_lastResponseBody);
			}

			[Test]
			public void request_succeeds() {
				Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
			}

			[Test]
			public void returns_correct_body() {
				var val = document.GetEntry()
					.Elements(XDocumentAtomExtensions.AtomNamespace + "content").First()
					.Element("data")
					.Element("a").Value;
				Assert.AreEqual(val, "1");
			}
		}

		[TestFixture]
		public class when_posting_an_event_as_raw_json_without_eventtype : HttpBehaviorSpecification {
			private HttpWebResponse _response;

			protected override void Given() {
			}

			protected override void When() {
				_response = MakeJsonPost(
					TestStream,
					new {A = "1", B = "3", C = "5"});
			}

			[Test]
			public void returns_bad_request_status_code() {
				Assert.AreEqual(HttpStatusCode.BadRequest, _response.StatusCode);
			}
		}

		[TestFixture]
		public class when_posting_an_event_to_idempotent_uri_as_events_array : HttpBehaviorSpecification {
			private HttpWebResponse _response;

			protected override void Given() {
			}

			protected override void When() {
				_response = MakeArrayEventsPost(
					TestStream + "/incoming/" + Guid.NewGuid().ToString(),
					new[] {new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {A = "1"}}});
			}

			[Test]
			public void returns_bad_request_status_code() {
				Assert.AreEqual(HttpStatusCode.UnsupportedMediaType, _response.StatusCode);
			}
		}

		[TestFixture]
		public class when_posting_an_event_as_json_to_idempotent_uri_without_event_type : HttpBehaviorSpecification {
			private HttpWebResponse _response;

			protected override void Given() {
			}

			protected override void When() {
				_response = MakeJsonPost(
					TestStream + "/incoming/" + Guid.NewGuid().ToString(),
					new {A = "1", B = "3", C = "5"});
			}

			[Test]
			public void returns_bad_request_status_code() {
				Assert.AreEqual(HttpStatusCode.BadRequest, _response.StatusCode);
			}
		}


		[TestFixture]
		public class when_posting_an_event_in_json_to_idempotent_uri_without_event_id : HttpBehaviorSpecification {
			private HttpWebResponse _response;

			protected override void Given() {
			}

			protected override void When() {
				var request = CreateRequest(TestStream + "/incoming/" + Guid.NewGuid().ToString(), "", "POST",
					"application/json");
				request.Headers.Add("ES-EventType", "SomeType");
				request.AllowAutoRedirect = false;
				var data = "{a : \"1\", b:\"3\", c:\"5\" }";
				var bytes = Encoding.UTF8.GetBytes(data);
				request.ContentLength = data.Length;
				request.GetRequestStream().Write(bytes, 0, data.Length);
				_response = GetRequestResponse(request);
			}

			[Test]
			public void returns_created_status_code() {
				Assert.AreEqual(HttpStatusCode.Created, _response.StatusCode);
			}

			[Test]
			public void returns_a_location_header() {
				Assert.That(!string.IsNullOrEmpty(_response.Headers[HttpResponseHeader.Location]));
			}

			[Test]
			public void returns_a_location_header_that_can_be_read_as_json() {
				var json = GetJson<JObject>(_response.Headers[HttpResponseHeader.Location]);
				HelperExtensions.AssertJson(new {a = "1", b = "3", c = "5"}, json);
			}
		}

		[TestFixture]
		public class when_posting_an_event_as_raw_json_without_eventid : HttpBehaviorSpecification {
			private HttpWebResponse _response;

			protected override void Given() {
			}

			protected override void When() {
				var request = CreateRequest(TestStream, "", "POST", "application/json");
				request.Headers.Add("ES-EventType", "SomeType");
				request.AllowAutoRedirect = false;
				var data = "{A : \"1\", B:\"3\", C:\"5\" }";
				var bytes = Encoding.UTF8.GetBytes(data);
				request.ContentLength = data.Length;
				request.GetRequestStream().Write(bytes, 0, data.Length);
				_response = GetRequestResponse(request);
			}

			[Test]
			public void returns_redirectkeepverb_status_code() {
				Assert.AreEqual(HttpStatusCode.RedirectKeepVerb, _response.StatusCode);
			}

			[Test]
			public void returns_a_location_header() {
				Assert.That(!string.IsNullOrEmpty(_response.Headers[HttpResponseHeader.Location]));
			}

			[Test]
			public void returns_a_to_incoming() {
				Assert.IsTrue(_response.Headers[HttpResponseHeader.Location].Contains("/incoming/"));
				//HelperExtensions.AssertJson(new {A = "1"}, json);
			}
		}

		[TestFixture]
		public class when_posting_an_event_as_array_with_no_event_type : HttpBehaviorSpecification {
			private HttpWebResponse _response;

			protected override void Given() {
			}

			protected override void When() {
				_response = MakeArrayEventsPost(
					TestStream,
					new[] {new {EventId = Guid.NewGuid(), Data = new {A = "1"}}});
			}

			[Test]
			public void returns_bad_request_status_code() {
				Assert.AreEqual(HttpStatusCode.BadRequest, _response.StatusCode);
			}
		}


		[TestFixture]
		public class when_posting_an_event_as_array : HttpBehaviorSpecification {
			private HttpWebResponse _response;

			protected override void Given() {
			}

			protected override void When() {
				_response = MakeArrayEventsPost(
					TestStream,
					new[] {new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {A = "1"}}});
			}

			[Test]
			public void returns_created_status_code() {
				Assert.AreEqual(HttpStatusCode.Created, _response.StatusCode);
			}

			[Test]
			public void returns_a_location_header() {
				Assert.IsNotEmpty(_response.Headers[HttpResponseHeader.Location]);
			}

			[Test]
			public void returns_a_location_header_that_can_be_read_as_json() {
				var json = GetJson<JObject>(_response.Headers[HttpResponseHeader.Location]);
				HelperExtensions.AssertJson(new {A = "1"}, json);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class when_posting_an_event_as_array_to_stream_with_slash : HttpBehaviorSpecification {
			private HttpWebResponse _response;

			protected override void Given() {
			}

			protected override void When() {
				var request = CreateRequest(TestStream + "/", "", "POST", "application/vnd.eventstore.events+json",
					null);
				request.AllowAutoRedirect = false;
				request.GetRequestStream()
					.WriteJson(new[] {new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {A = "1"}}});
				_response = (HttpWebResponse)request.GetResponse();
			}

			[Test]
			public void returns_permanent_redirect() {
				Assert.AreEqual(HttpStatusCode.RedirectKeepVerb, _response.StatusCode);
			}

			[Test]
			public void redirect_is_cacheable() {
				Assert.AreEqual("max-age=31536000, public", _response.Headers[HttpResponseHeader.CacheControl]);
			}

			[Test]
			public void returns_a_location_header() {
				Assert.IsNotEmpty(_response.Headers[HttpResponseHeader.Location]);
			}

			[Test]
			public void returns_a_location_header_that_is_to_stream_without_slash() {
				Assert.AreEqual(MakeUrl(TestStream), _response.Headers[HttpResponseHeader.Location]);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class when_deleting_to_stream_with_slash : HttpBehaviorSpecification {
			private HttpWebResponse _response;

			protected override void Given() {
			}

			protected override void When() {
				var request = CreateRequest(TestStream + "/", "", "DELETE", "application/json", null);
				request.AllowAutoRedirect = false;
				_response = (HttpWebResponse)request.GetResponse();
			}

			[Test]
			public void returns_permanent_redirect() {
				Assert.AreEqual(HttpStatusCode.RedirectKeepVerb, _response.StatusCode);
			}

			[Test]
			public void returns_a_location_header() {
				Assert.IsNotEmpty(_response.Headers[HttpResponseHeader.Location]);
			}

			[Test]
			public void returns_a_location_header_that_is_to_stream_without_slash() {
				Assert.AreEqual(MakeUrl(TestStream), _response.Headers[HttpResponseHeader.Location]);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class when_getting_from_stream_with_slash : HttpBehaviorSpecification {
			private HttpWebResponse _response;

			protected override void Given() {
			}

			protected override void When() {
				var request = CreateRequest(TestStream + "/", "", "GET", "application/json", null);
				request.AllowAutoRedirect = false;
				_response = (HttpWebResponse)request.GetResponse();
			}

			[Test]
			public void returns_permanent_redirect() {
				Assert.AreEqual(HttpStatusCode.RedirectKeepVerb, _response.StatusCode);
			}

			[Test]
			public void returns_a_location_header() {
				Assert.IsNotEmpty(_response.Headers[HttpResponseHeader.Location]);
			}

			[Test]
			public void returns_a_location_header_that_is_to_stream_without_slash() {
				Assert.AreEqual(MakeUrl(TestStream), _response.Headers[HttpResponseHeader.Location]);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class when_getting_from_all_stream_with_slash : HttpBehaviorSpecification {
			private HttpWebResponse _response;

			protected override void Given() {
			}

			protected override void When() {
				var request = CreateRequest("/streams/$all/", "", "GET", "application/json", null);
				request.Credentials = DefaultData.AdminNetworkCredentials;
				request.AllowAutoRedirect = false;
				_response = (HttpWebResponse)request.GetResponse();
			}

			[Test]
			public void returns_permanent_redirect() {
				Assert.AreEqual(HttpStatusCode.RedirectKeepVerb, _response.StatusCode);
			}

			[Test]
			public void returns_a_location_header() {
				Assert.IsNotEmpty(_response.Headers[HttpResponseHeader.Location]);
			}

			[Test]
			public void returns_a_location_header_that_is_to_stream_without_slash() {
				Assert.AreEqual(MakeUrl("/streams/$all"), _response.Headers[HttpResponseHeader.Location]);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class when_getting_from_encoded_all_stream_with_slash : HttpBehaviorSpecification {
			private HttpWebResponse _response;

			protected override void Given() {
			}

			protected override void When() {
				var request = CreateRequest("/streams/%24all/", "", "GET", "application/json", null);
				request.Credentials = DefaultData.AdminNetworkCredentials;
				request.AllowAutoRedirect = false;
				_response = (HttpWebResponse)request.GetResponse();
			}

			[Test]
			public void returns_permanent_redirect() {
				Assert.AreEqual(HttpStatusCode.RedirectKeepVerb, _response.StatusCode);
			}

			[Test]
			public void returns_a_location_header() {
				Assert.IsNotEmpty(_response.Headers[HttpResponseHeader.Location]);
			}

			[Test]
			public void returns_a_location_header_that_is_to_stream_without_slash() {
				Assert.AreEqual(MakeUrl("/streams/$all").ToString(), _response.Headers[HttpResponseHeader.Location]);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class when_posting_an_event_as_array_to_metadata_stream_with_slash : HttpBehaviorSpecification {
			private HttpWebResponse _response;

			protected override void Given() {
			}

			protected override void When() {
				var request = CreateRequest(TestStream + "/metadata/", "", "POST", "application/json", null);
				request.AllowAutoRedirect = false;
				request.GetRequestStream()
					.WriteJson(new[] {new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {A = "1"}}});
				_response = (HttpWebResponse)request.GetResponse();
			}

			[Test]
			public void returns_permanent_redirect() {
				Assert.AreEqual(HttpStatusCode.RedirectKeepVerb, _response.StatusCode);
			}

			[Test]
			public void returns_a_location_header() {
				Assert.IsNotEmpty(_response.Headers[HttpResponseHeader.Location]);
			}

			[Test]
			public void returns_a_location_header_that_is_to_stream_without_slash() {
				Assert.AreEqual(MakeUrl(TestStream + "/metadata"), _response.Headers[HttpResponseHeader.Location]);
			}
		}


		[TestFixture, Category("LongRunning")]
		public class when_getting_from_metadata_stream_with_slash : HttpBehaviorSpecification {
			private HttpWebResponse _response;

			protected override void Given() {
			}

			protected override void When() {
				var request = CreateRequest(TestStream + "/metadata/", "", "GET", "application/json", null);
				request.Credentials = DefaultData.AdminNetworkCredentials;
				request.AllowAutoRedirect = false;
				_response = (HttpWebResponse)request.GetResponse();
			}

			[Test]
			public void returns_permanent_redirect() {
				Assert.AreEqual(HttpStatusCode.RedirectKeepVerb, _response.StatusCode);
			}

			[Test]
			public void returns_a_location_header() {
				Assert.IsNotEmpty(_response.Headers[HttpResponseHeader.Location]);
			}

			[Test]
			public void returns_a_location_header_that_is_to_stream_without_slash() {
				Assert.AreEqual(MakeUrl(TestStream + "/metadata"), _response.Headers[HttpResponseHeader.Location]);
			}
		}


		[TestFixture, Category("LongRunning")]
		public class when_posting_an_event_without_EventId_as_array : HttpBehaviorSpecification {
			private HttpWebResponse _response;

			protected override void Given() {
			}

			protected override void When() {
				_response = MakeJsonPost(
					TestStream,
					new[] {new {EventType = "event-type", Data = new {A = "1"}}});
			}

			[Test]
			public void returns_bad_request_status_code() {
				Assert.AreEqual(HttpStatusCode.BadRequest, _response.StatusCode);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class when_posting_an_event_without_EventType_as_array : HttpBehaviorSpecification {
			private HttpWebResponse _response;

			protected override void Given() {
			}

			protected override void When() {
				_response = MakeArrayEventsPost(
					TestStream,
					new[] {new {EventId = Guid.NewGuid(), Data = new {A = "1"}}});
			}

			[Test]
			public void returns_bad_request_status_code() {
				Assert.AreEqual(HttpStatusCode.BadRequest, _response.StatusCode);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class when_posting_an_event_with_date_time : HttpBehaviorSpecification {
			private HttpWebResponse _response;

			protected override void Given() {
			}

			protected override void When() {
				_response = MakeArrayEventsPost(
					TestStream,
					new[] {
						new {
							EventId = Guid.NewGuid(),
							EventType = "event-type",
							Data = new {A = "1987-11-07T00:00:00.000+01:00"}
						},
					});
			}

			[Test]
			public void returns_created_status_code() {
				Assert.AreEqual(HttpStatusCode.Created, _response.StatusCode);
			}

			[Test]
			public void returns_a_location_header() {
				Assert.IsNotEmpty(_response.Headers[HttpResponseHeader.Location]);
			}

			[Test]
			public void the_json_data_is_not_mangled() {
				var json = GetJson<JObject>(_response.Headers[HttpResponseHeader.Location]);
				HelperExtensions.AssertJson(new {A = "1987-11-07T00:00:00.000+01:00"}, json);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class when_posting_an_events_as_array : HttpBehaviorSpecification {
			private HttpWebResponse _response;

			protected override void Given() {
			}

			protected override void When() {
				_response = MakeArrayEventsPost(
					TestStream,
					new[] {
						new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {A = "1"}},
						new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {A = "2"}},
					});
			}

			[Test]
			public void returns_created_status_code() {
				Assert.AreEqual(HttpStatusCode.Created, _response.StatusCode);
			}

			[Test]
			public void returns_a_location_header() {
				Assert.IsNotEmpty(_response.Headers[HttpResponseHeader.Location]);
			}

			[Test]
			public void returns_a_location_header_for_the_first_posted_event() {
				var json = GetJson<JObject>(_response.Headers[HttpResponseHeader.Location]);
				HelperExtensions.AssertJson(new {A = "1"}, json);
			}
		}

		public abstract class HttpBehaviorSpecificationWithSingleEvent : HttpBehaviorSpecification {
			protected HttpWebResponse _response;

			protected override void Given() {
				_response = MakeArrayEventsPost(
					TestStream,
					new[] {new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {A = "1"}}});
				Assert.AreEqual(HttpStatusCode.Created, _response.StatusCode);
			}
		}


		[TestFixture, Category("LongRunning")]
		public class
			when_requesting_a_single_event_that_is_deleted_linkto : HttpSpecificationWithLinkToToDeletedEvents {
			protected override void When() {
				Get("/streams/" + LinkedStreamName + "/0", "", "application/json");
			}

			[Test]
			public void the_event_is_gone() {
				Assert.AreEqual(HttpStatusCode.NotFound, _lastResponse.StatusCode);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class
			when_requesting_a_single_event_that_is_maxcount_deleted_linkto :
				SpecificationWithLinkToToMaxCountDeletedEvents {
			protected override void When() {
				Get("/streams/" + LinkedStreamName + "/0", "", "application/json");
			}

			[Test]
			public void the_event_is_gone() {
				Assert.AreEqual(HttpStatusCode.NotFound, _lastResponse.StatusCode);
			}
		}


		[TestFixture, Category("LongRunning")]
		public class
			when_requesting_a_single_event_in_the_stream_without_an_accept_header :
				HttpBehaviorSpecificationWithSingleEvent {
			private XDocument _xmlDocument;

			protected override void When() {
				_xmlDocument = GetXml(MakeUrl(TestStream + "/0"));
			}

			[Test]
			public void request_succeeds() {
				Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
			}

			[Test]
			public void returns_correct_body() {
				Assert.AreEqual("1", _xmlDocument.Element("data").Element("a").Value);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class
			when_requesting_a_single_event_in_the_stream_as_event_json : HttpBehaviorSpecificationWithSingleEvent {
			private JObject _json;

			protected override void When() {
				_json = GetJson<JObject>(TestStream + "/0", accept: ContentType.EventJson);
			}

			[Test]
			public void request_succeeds() {
				Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
			}

			[Test]
			public void returns_correct_body() {
				HelperExtensions.AssertJson(new {Data = new {A = "1"}}, _json);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class when_requesting_a_single_event_in_the_stream_as_json : HttpBehaviorSpecificationWithSingleEvent {
			private JObject _json;

			protected override void When() {
				_json = GetJson<JObject>(TestStream + "/0", accept: ContentType.Json);
			}

			[Test]
			public void request_succeeds() {
				Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
			}

			[Test]
			public void returns_correct_body() {
				HelperExtensions.AssertJson(new {A = "1"}, _json);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class
			when_requesting_a_single_event_in_the_stream_as_event_xml : HttpBehaviorSpecificationWithSingleEvent {
			protected override void When() {
				Get(TestStream + "/0", "", accept: ContentType.EventXml);
			}

			[Test]
			public void request_succeeds() {
				Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class when_requesting_a_single_event_in_the_stream_as_xml : HttpBehaviorSpecificationWithSingleEvent {
			protected override void When() {
				Get(TestStream + "/0", "", accept: ContentType.Xml);
			}

			[Test]
			public void request_succeeds() {
				Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
			}
		}

		[TestFixture]
		public class when_requesting_a_single_raw_event_in_the_stream_as_raw : HttpBehaviorSpecification {
			protected HttpWebResponse _response;
			protected byte[] _data;

			protected override void Given() {
				var request = CreateRequest(TestStream, String.Empty, HttpMethod.Post, "application/octet-stream");
				request.Headers.Add("ES-EventType", "TestEventType");
				request.Headers.Add("ES-EventID", Guid.NewGuid().ToString());
				if (_data == null) {
					var fileData = HelperExtensions.GetFilePathFromAssembly("Resources/es-tile.png");
					_data = File.ReadAllBytes(fileData);
				}

				request.ContentLength = _data.Length;
				request.GetRequestStream().Write(_data, 0, _data.Length);
				_response = GetRequestResponse(request);
				Assert.AreEqual(HttpStatusCode.Created, _response.StatusCode);
			}

			protected override void When() {
				Get(TestStream + "/0", "", "application/octet-stream");
			}

			[Test]
			public void returns_correct_body() {
				Assert.AreEqual(_data, _lastResponseBytes);
			}
		}
	}
}
