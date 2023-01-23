using System.Net;
using System.Threading.Tasks;
using EventStore.Core.Services.Transport.Http.Authentication;
using EventStore.Core.Services.UserManagement;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Transport.Http.Authentication {
	public class TestFixtureWithUnixSocketAuthenticationProvider {
		protected UnixSocketAuthenticationProvider _provider;

		protected void SetUpProvider() {
			_provider = new UnixSocketAuthenticationProvider();
		}
	}

	[TestFixture]
	public class
		when_handling_a_request_having_a_remote_ip_address :
			TestFixtureWithUnixSocketAuthenticationProvider {
		private bool _authenticateResult;

		[SetUp]
		public void SetUp() {
			SetUpProvider();
			var context = new DefaultHttpContext {
				Connection = { RemoteIpAddress = IPAddress.Loopback }
			};

			_authenticateResult = _provider.Authenticate(context, out _);
		}

		[Test]
		public void returns_false() {
			Assert.IsFalse(_authenticateResult);
		}
	}

	[TestFixture]
	public class
		when_handling_a_request_not_having_a_remote_ip_address :
			TestFixtureWithUnixSocketAuthenticationProvider {
		private bool _authenticateResult;
		private HttpAuthenticationRequest _authenticateRequest;

		[SetUp]
		public void SetUp() {
			SetUpProvider();
			var context = new DefaultHttpContext();
			Assert.IsNull(context.Connection.RemoteIpAddress);
			_authenticateResult = _provider.Authenticate(context, out _authenticateRequest);
		}

		[Test]
		public void returns_true() {
			Assert.IsTrue(_authenticateResult);
		}

		[Test]
		public async Task passes_authentication() {
			var (status, _) = await _authenticateRequest.AuthenticateAsync();
			Assert.AreEqual(HttpAuthenticationRequestStatus.Authenticated, status);
		}

		[Test]
		public async Task sets_user_to_system_user() {
			var (_, user) = await _authenticateRequest.AuthenticateAsync();
			Assert.AreEqual(SystemAccounts.System.Claims, user.Claims);
		}
	}
}
