// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

#nullable enable
using System.Linq;
using System.Threading.Tasks;

namespace EventStore.Core.Authorization.AuthorizationPolicies;

public class StaticAuthorizationPolicyRegistry : IAuthorizationPolicyRegistry {
	public Task Start() => Task.CompletedTask;
	public Task Stop() => Task.CompletedTask;
	private readonly IPolicySelector[] _policySelectors;
	public ReadOnlyPolicy[] EffectivePolicies => _policySelectors.Select(x => x.Select()).ToArray();

	public StaticAuthorizationPolicyRegistry(IPolicySelector[] policySelectors) {
		_policySelectors = policySelectors;
	}
}
