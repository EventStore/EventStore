using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using EventStore.Core.Services.Transport.Http.Authentication;
using EventStore.Core.Services.UserManagement;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Transport.Http.Authentication {
	public class TestFixtureWithClientCertificateHttpAuthenticationProvider {
		protected ClientCertificateAuthenticationProvider _provider;

		protected void SetUpProvider() {
			_provider = new ClientCertificateAuthenticationProvider();
		}
	}

	[TestFixture]
	public class
		when_handling_a_request_without_a_client_certificate :
			TestFixtureWithClientCertificateHttpAuthenticationProvider {
		private bool _authenticateResult;

		[SetUp]
		public void SetUp() {
			SetUpProvider();
			var context = new DefaultHttpContext();
			Assert.IsNull(context.Connection.ClientCertificate);
			_authenticateResult = _provider.Authenticate(context, out _);
		}

		[Test]
		public void returns_false() {
			Assert.IsFalse(_authenticateResult);
		}
	}

	[TestFixture]
	public class
		when_handling_a_request_with_a_client_certificate :
			TestFixtureWithClientCertificateHttpAuthenticationProvider {
		private HttpAuthenticationRequest _authenticateRequest;
		private bool _authenticateResult;
		private HttpContext _context;

		[SetUp]
		public void SetUp() {
			SetUpProvider();
			_context = new DefaultHttpContext();
			_context.Connection.ClientCertificate = new X509Certificate2();
			_authenticateResult = _provider.Authenticate(_context, out _authenticateRequest);
		}

		[Test]
		public void returns_true() {
			Assert.IsTrue(_authenticateResult);
		}

		[Test]
		public async Task passes_authentication() {
			Assert.IsTrue(await _authenticateRequest.AuthenticateAsync());
		}

		[Test]
		public void sets_user_to_system_user() {
			Assert.AreEqual(SystemAccounts.System.Claims, _context.User.Claims);
		}
	}
}
