using System;
using System.Linq;
using EventStore.Plugins.Authorization;

namespace EventStore.Core.Authorization {
	public class AuthorizationProviderFactory {
		private readonly Func<AuthorizationProviderFactoryComponents, IAuthorizationProviderFactory>
			_authorizationProviderFactory;

		public AuthorizationProviderFactory(
			Func<AuthorizationProviderFactoryComponents, IAuthorizationProviderFactory>
				authorizationProviderFactory) {
			_authorizationProviderFactory = authorizationProviderFactory;
		}

		public IAuthorizationProviderFactory GetFactory(
			AuthorizationProviderFactoryComponents authorizationProviderFactoryComponents) =>
			_authorizationProviderFactory(authorizationProviderFactoryComponents);
	}

	public class AuthorizationPolicySelectorsFactory {
		private readonly Func<AuthorizationProviderFactoryComponents, IAuthorizationPolicySelectorFactory>[]
			_authorizationPolicySelectorFactories;

		public AuthorizationPolicySelectorsFactory(params
			Func<AuthorizationProviderFactoryComponents, IAuthorizationPolicySelectorFactory>[]
				authorizationPolicySelectorFactory) {
			_authorizationPolicySelectorFactories = authorizationPolicySelectorFactory;
		}

		public IPolicySelector[] Create(
			AuthorizationProviderFactoryComponents authorizationProviderFactoryComponents) =>
			_authorizationPolicySelectorFactories
					.Select(p => p(authorizationProviderFactoryComponents).Build())
					.ToArray();
	}
}
