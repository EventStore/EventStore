#nullable enable

using System.Collections.Generic;
using EventStore.Core.Services.UserManagement;
using EventStore.Plugins.Authentication;

namespace EventStore.Core.Authentication.PassthroughAuthentication;

public class PassthroughAuthenticationProvider() : AuthenticationProviderBase(name: "insecure")  {
	public override void Authenticate(AuthenticationRequest authenticationRequest) =>
		authenticationRequest.Authenticated(SystemAccounts.System);

	public override IReadOnlyList<string> GetSupportedAuthenticationSchemes() => ["Insecure"];
}