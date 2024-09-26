// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Authorization.AuthorizationPolicies;
using EventStore.Plugins.Authorization;

namespace EventStore.Core.Authorization;

public class InternalAuthorizationProviderFactory : IAuthorizationProviderFactory {
	private readonly IAuthorizationPolicyRegistry _registry;

	public InternalAuthorizationProviderFactory(IAuthorizationPolicyRegistry registry) {
		_registry = registry;
	}

	public IAuthorizationProvider Build() {
		return new PolicyAuthorizationProvider(
		new MultiPolicyEvaluator(_registry), logAuthorization: true, logSuccesses: false);
	}
}
