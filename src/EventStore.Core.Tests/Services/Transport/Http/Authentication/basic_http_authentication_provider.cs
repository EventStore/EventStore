using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Claims;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Http.Authentication;
using EventStore.Core.Services.Transport.Http.Messages;
using EventStore.Core.Tests.Authentication;
using EventStore.Transport.Http.EntityManagement;
using NUnit.Framework;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace EventStore.Core.Tests.Services.Transport.Http.Authentication {
	namespace basic_http_authentication_provider {
		public abstract class TestFixtureWithBasicHttpAuthenticationProvider<TLogFormat, TStreamId> : with_internal_authentication_provider<TLogFormat, TStreamId> {
			protected BasicHttpAuthenticationProvider _provider;

			protected new void SetUpProvider() {
				base.SetUpProvider();
				_provider = new BasicHttpAuthenticationProvider(_internalAuthenticationProvider);
			}

			protected static HttpContext CreateTestEntityWithCredentials(string username, string password) {
				var context = new DefaultHttpContext();
				context.Request.Headers.Add("authorization",
					"Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}")));
				return context;
			}
		}

		[TestFixture(typeof(LogFormat.V2), typeof(string))]
		[TestFixture(typeof(LogFormat.V3), typeof(long))]
		public class
			when_handling_a_request_without_an_authorization_header<TLogFormat, TStreamId> : TestFixtureWithBasicHttpAuthenticationProvider<TLogFormat, TStreamId> {
			private bool _authenticateResult;

			[SetUp]
			public void SetUp() {
				SetUpProvider();
				var context = new DefaultHttpContext();
				_authenticateResult = _provider.Authenticate(context, out _);
			}

			[Test]
			public void returns_false() {
				Assert.IsFalse(_authenticateResult);
			}
		}

		[TestFixture(typeof(LogFormat.V2), typeof(string))]
		[TestFixture(typeof(LogFormat.V3), typeof(long))]
		public class
			when_handling_a_request_with_correct_user_name_and_password<TLogFormat, TStreamId> :
				TestFixtureWithBasicHttpAuthenticationProvider<TLogFormat, TStreamId> {
			private bool _authenticateResult;
			private HttpAuthenticationRequest _request;
			private HttpContext _context;

			protected override void Given() {
				base.Given();
				ExistingEvent("$user-user", "$user", null, "{LoginName:'user', Salt:'drowssap',Hash:'password'}");
			}

			[SetUp]
			public void SetUp() {
				SetUpProvider();
				_context = CreateTestEntityWithCredentials("user", "password");
				_authenticateResult = _provider.Authenticate(_context, out _request);
			}

			[Test]
			public void returns_true() {
				Assert.IsTrue(_authenticateResult);
				Assert.NotNull(_request);
			}

			[Test]
			public async Task ShouldAuthenticateUser() {
				Assert.True(await _request.AuthenticateAsync());
				Assert.NotNull(_context.User);
				Assert.AreEqual("user", _context?.User?.Identity?.Name);
			}
		}

		[TestFixture(typeof(LogFormat.V2), typeof(string))]
		[TestFixture(typeof(LogFormat.V3), typeof(long))]
		public class
			when_handling_a_request_with_incorrect_user_name_and_password<TLogFormat, TStreamId> :
				TestFixtureWithBasicHttpAuthenticationProvider<TLogFormat, TStreamId> {
			private bool _authenticateResult;
			private HttpAuthenticationRequest _request;
			private HttpContext _context;

			protected override void Given() {
				base.Given();
				ExistingEvent("$user-user", "$user", null, "{LoginName:'user', Salt:'drowssap',Hash:'password'}");
			}

			[SetUp]
			public void SetUp() {
				SetUpProvider();
				_context = CreateTestEntityWithCredentials("user", "password1");
				_authenticateResult = _provider.Authenticate(_context, out _request);
			}

			[Test]
			public void returns_true() {
				Assert.IsTrue(_authenticateResult);
			}

			[Test]
			public async Task ShouldNotBeAuthenticated() {
				Assert.False(await _request.AuthenticateAsync());

				Assert.IsEmpty(_context.User.Claims);
			}
		}
	}
}
