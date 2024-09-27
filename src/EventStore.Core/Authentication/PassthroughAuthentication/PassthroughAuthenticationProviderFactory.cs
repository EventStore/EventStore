// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Plugins.Authentication;

namespace EventStore.Core.Authentication.PassthroughAuthentication;

public class PassthroughAuthenticationProviderFactory : IAuthenticationProviderFactory {
	public IAuthenticationProvider Build(bool logFailedAuthenticationAttempts) => new PassthroughAuthenticationProvider();
}
