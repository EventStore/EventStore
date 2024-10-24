// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

#nullable enable

using System.Collections.Generic;
using EventStore.Core.Services.UserManagement;
using EventStore.Plugins.Authentication;

namespace EventStore.Core.Authentication.PassthroughAuthentication;

public class PassthroughAuthenticationProvider() : AuthenticationProviderBase(name: "insecure", diagnosticsName: "PassthroughAuthentication") {
	public override void Authenticate(AuthenticationRequest authenticationRequest) =>
		authenticationRequest.Authenticated(SystemAccounts.System);

	public override IReadOnlyList<string> GetSupportedAuthenticationSchemes() => ["Insecure"];
}
