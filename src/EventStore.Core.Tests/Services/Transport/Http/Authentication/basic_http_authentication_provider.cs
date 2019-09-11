using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Principal;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Http.Authentication;
using EventStore.Core.Services.Transport.Http.Messages;
using EventStore.Core.Tests.Authentication;
using EventStore.Transport.Http.EntityManagement;
using NUnit.Framework;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace EventStore.Core.Tests.Services.Transport.Http.Authentication {
	namespace basic_http_authentication_provider {
		public class TestFixtureWithBasicHttpAuthenticationProvider : with_internal_authentication_provider {
			protected BasicHttpAuthenticationProvider _provider;
			protected HttpEntity _entity;

			protected new void SetUpProvider() {
				base.SetUpProvider();
				_provider = new BasicHttpAuthenticationProvider(_internalAuthenticationProvider);
			}

			protected static HttpEntity CreateTestEntityWithCredentials(string username, string password) =>
				new HttpEntity(new FakeHttpRequest(username, password), new FakeHttpResponse(), null, false,
					IPAddress.Any, 0,
					() => { });

			protected static HttpEntity CreateTestEntityWithoutCredentials()
				=> new HttpEntity(new FakeHttpRequest(), new FakeHttpResponse(), null, false,
					IPAddress.Any, 0,
					() => { });
		}

		[TestFixture]
		public class
			when_handling_a_request_without_an_authorization_header : TestFixtureWithBasicHttpAuthenticationProvider {
			private bool _authenticateResult;

			[SetUp]
			public void SetUp() {
				SetUpProvider();
				_entity = CreateTestEntityWithoutCredentials();
				_authenticateResult = _provider.Authenticate(new IncomingHttpRequestMessage(null, _entity, _bus));
			}

			[Test]
			public void returns_false() {
				Assert.IsFalse(_authenticateResult);
			}

			[Test]
			public void does_not_publish_authenticated_http_request_message() {
				var authenticatedHttpRequestMessages =
					_consumer.HandledMessages.OfType<AuthenticatedHttpRequestMessage>().ToList();
				Assert.AreEqual(0, authenticatedHttpRequestMessages.Count);
			}
		}

		[TestFixture]
		public class
			when_handling_a_request_with_correct_user_name_and_password :
				TestFixtureWithBasicHttpAuthenticationProvider {
			private bool _authenticateResult;

			protected override void Given() {
				base.Given();
				ExistingEvent("$user-user", "$user", null, "{LoginName:'user', Salt:'drowssap',Hash:'password'}");
			}

			[SetUp]
			public void SetUp() {
				SetUpProvider();
				_entity = CreateTestEntityWithCredentials("user", "password");
				_authenticateResult = _provider.Authenticate(new IncomingHttpRequestMessage(null, _entity, _bus));
			}

			[Test]
			public void returns_true() {
				Assert.IsTrue(_authenticateResult);
			}

			[Test]
			public void publishes_authenticated_http_request_message_with_user() {
				var authenticatedHttpRequestMessages =
					_consumer.HandledMessages.OfType<AuthenticatedHttpRequestMessage>().ToList();
				Assert.AreEqual(1, authenticatedHttpRequestMessages.Count);
				var message = authenticatedHttpRequestMessages[0];
				Assert.AreEqual("user", message.Entity.User.Identity.Name);
				Assert.IsTrue(message.Entity.User.Identity.IsAuthenticated);
			}
		}

		[TestFixture]
		public class
			when_handling_multiple_requests_with_the_same_correct_user_name_and_password :
				TestFixtureWithBasicHttpAuthenticationProvider {
			private bool _authenticateResult;

			protected override void Given() {
				base.Given();
				ExistingEvent("$user-user", "$user", null, "{LoginName:'user', Salt:'drowssap',Hash:'password'}");
			}

			[SetUp]
			public void SetUp() {
				SetUpProvider();

				var entity = CreateTestEntityWithCredentials("user", "password");
				_provider.Authenticate(new IncomingHttpRequestMessage(null, entity, _bus));

				_consumer.HandledMessages.Clear();

				_entity = CreateTestEntityWithCredentials("user", "password");
				_authenticateResult = _provider.Authenticate(new IncomingHttpRequestMessage(null, _entity, _bus));
			}

			[Test]
			public void returns_true() {
				Assert.IsTrue(_authenticateResult);
			}

			[Test]
			public void publishes_authenticated_http_request_message_with_user() {
				var authenticatedHttpRequestMessages =
					_consumer.HandledMessages.OfType<AuthenticatedHttpRequestMessage>().ToList();
				Assert.AreEqual(1, authenticatedHttpRequestMessages.Count);
				var message = authenticatedHttpRequestMessages[0];
				Assert.AreEqual("user", message.Entity.User.Identity.Name);
				Assert.IsTrue(message.Entity.User.Identity.IsAuthenticated);
			}

			[Test]
			public void does_not_publish_any_read_requests() {
				Assert.AreEqual(0, _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsBackward>().Count());
				Assert.AreEqual(0, _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>().Count());
			}
		}

		[TestFixture]
		public class
			when_handling_a_request_with_incorrect_user_name_and_password :
				TestFixtureWithBasicHttpAuthenticationProvider {
			private bool _authenticateResult;

			protected override void Given() {
				base.Given();
				ExistingEvent("$user-user", "$user", null, "{LoginName:'user', Salt:'drowssap',Hash:'password'}");
			}

			[SetUp]
			public void SetUp() {
				SetUpProvider();
				_entity = CreateTestEntityWithCredentials("user", "password1");
				_authenticateResult = _provider.Authenticate(new IncomingHttpRequestMessage(null, _entity, _bus));
			}

			[Test]
			public void returns_true() {
				Assert.IsTrue(_authenticateResult);
			}

			[Test]
			public void publishes_authenticated_http_request_message_with_user() {
				var authenticatedHttpRequestMessages =
					_consumer.HandledMessages.OfType<AuthenticatedHttpRequestMessage>().ToList();
				Assert.AreEqual(0, authenticatedHttpRequestMessages.Count);
			}
		}

		class FakeHttpRequest : IHttpRequest {
			private readonly HeaderDictionary _headers;

			public FakeHttpRequest() {
				_headers = new HeaderDictionary();
			}

			public FakeHttpRequest(string username, string password) {
				_headers = new HeaderDictionary {
					{
						"authorization",
						"Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"))
					}
				};
			}

			public string[] AcceptTypes { get; }
			public long ContentLength64 { get; }
			public string ContentType { get; }
			public string HttpMethod { get; }
			public Stream InputStream { get; }
			public string RawUrl { get; }
			public IPEndPoint RemoteEndPoint { get; }
			public Uri Url { get; } = new UriBuilder().Uri;
			public IEnumerable<string> GetHeaderKeys() => _headers.Keys;

			public StringValues GetHeaderValues(string key) => _headers[key];

			public IEnumerable<string> GetQueryStringKeys() {
				throw new NotImplementedException();
			}

			public StringValues GetQueryStringValues(string key) {
				throw new NotImplementedException();
			}
		}

		class FakeHttpResponse : IHttpResponse {
			public void AddHeader(string name, string value) {
				throw new NotImplementedException();
			}

			public void Close() {
				throw new NotImplementedException();
			}

			public long ContentLength64 { get; set; }
			public string ContentType { get; set; }
			public Stream OutputStream { get; }
			public int StatusCode { get; set; }
			public string StatusDescription { get; set; }
		}
	}
}
