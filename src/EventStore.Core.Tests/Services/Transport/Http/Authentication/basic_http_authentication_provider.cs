using System.Net;
using System.Security.Principal;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Http.Authentication;
using EventStore.Core.Services.Transport.Http.Messages;
using EventStore.Core.Tests.Authentication;
using EventStore.Transport.Http.EntityManagement;
using NUnit.Framework;
using System.Linq;

namespace EventStore.Core.Tests.Services.Transport.Http.Authentication {
	namespace basic_http_authentication_provider {
		public class TestFixtureWithBasicHttpAuthenticationProvider : with_internal_authentication_provider {
			protected BasicHttpAuthenticationProvider _provider;
			protected HttpEntity _entity;

			protected new void SetUpProvider() {
				base.SetUpProvider();
				_provider = new BasicHttpAuthenticationProvider(_internalAuthenticationProvider);
			}
		}

		[TestFixture]
		public class
			when_handling_a_request_without_an_authorization_header : TestFixtureWithBasicHttpAuthenticationProvider {
			private bool _authenticateResult;

			[SetUp]
			public void SetUp() {
				SetUpProvider();
				_entity = HttpEntity.Test(null);
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
				_entity = HttpEntity.Test(new GenericPrincipal(new HttpListenerBasicIdentity("user", "password"),
					new string[0]));
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

				var entity = HttpEntity.Test(new GenericPrincipal(new HttpListenerBasicIdentity("user", "password"),
					new string[0]));
				_provider.Authenticate(new IncomingHttpRequestMessage(null, entity, _bus));

				_consumer.HandledMessages.Clear();

				_entity = HttpEntity.Test(new GenericPrincipal(new HttpListenerBasicIdentity("user", "password"),
					new string[0]));
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
				_entity = HttpEntity.Test(new GenericPrincipal(new HttpListenerBasicIdentity("user", "password1"),
					new string[0]));
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
	}
}
