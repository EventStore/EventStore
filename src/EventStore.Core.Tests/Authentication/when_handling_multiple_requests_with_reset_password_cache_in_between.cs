using System.Linq;
using System.Security.Principal;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Authentication {
	[TestFixture]
	public class when_handling_multiple_requests_with_reset_password_cache_in_between :
		with_internal_authentication_provider {
		private bool _unauthorized;
		private IPrincipal _authenticatedAs;
		private bool _error;

		protected override void Given() {
			base.Given();
			ExistingEvent("$user-user", "$user", null, "{LoginName:'user', Salt:'drowssap',Hash:'password'}");
		}

		[SetUp]
		public void SetUp() {
			SetUpProvider();

			_internalAuthenticationProvider.Authenticate(
				new TestAuthenticationRequest("user", "password", () => { }, p => { }, () => { }, () => { }));
			_internalAuthenticationProvider.Handle(
				new InternalAuthenticationProviderMessages.ResetPasswordCache("user"));
			_consumer.HandledMessages.Clear();

			_internalAuthenticationProvider.Authenticate(
				new TestAuthenticationRequest(
					"user", "password", () => _unauthorized = true, p => _authenticatedAs = p, () => _error = true,
					() => { }));
		}

		[Test]
		public void authenticates_user() {
			Assert.IsFalse(_unauthorized);
			Assert.IsFalse(_error);
			Assert.NotNull(_authenticatedAs);
			Assert.IsTrue(_authenticatedAs.IsInRole("user"));
		}

		[Test]
		public void publishes_some_read_requests() {
			Assert.Greater(
				_consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsBackward>().Count()
				+ _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>().Count(), 0);
		}
	}
}
