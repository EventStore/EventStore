using System.Collections.Generic;
using System.Security.Claims;
using EventStore.Core.Authentication;
using EventStore.Plugins.Authentication;
using Microsoft.Extensions.Logging;

namespace EventStore.Core.Tests.Common.ClusterNodeOptionsTests;

public class TestAuthenticationProviderFactory : IAuthenticationProviderFactory {
	public IAuthenticationProvider Build(bool logFailedAuthenticationAttempts, ILogger logger) => 
		new TestAuthenticationProvider();
}

public class TestAuthenticationProvider() : AuthenticationProviderBase(name: "test") {
	public override void Authenticate(AuthenticationRequest authenticationRequest) => 
		authenticationRequest.Authenticated(
			new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, authenticationRequest.Name) }))
		);

	public override IReadOnlyList<string> GetSupportedAuthenticationSchemes() => ["Basic"];
}