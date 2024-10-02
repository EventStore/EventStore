// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Plugins.Authorization;

namespace EventStore.Core.Authorization {
	public class InternalAuthorizationProviderFactory : IAuthorizationProviderFactory {
		private readonly IPolicySelector[] _policySelectors;

		public InternalAuthorizationProviderFactory(IPolicySelector[] policySelectors) {
			_policySelectors = policySelectors;
		}

		public IAuthorizationProvider Build() {
			return new PolicyAuthorizationProvider(
			new MultiPolicyEvaluator(_policySelectors), logAuthorization: true, logSuccesses: false);
		}
	}
}
