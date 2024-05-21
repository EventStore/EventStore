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

	public class AuthorizationPolicyFactory {
		private readonly Func<AuthorizationProviderFactoryComponents, IAuthorizationPolicyFactory>[]
			_authorizationPolicyFactories;

		public AuthorizationPolicyFactory(params
			Func<AuthorizationProviderFactoryComponents, IAuthorizationPolicyFactory>[]
				authorizationPolicyFactory) {
			_authorizationPolicyFactories = authorizationPolicyFactory;
		}

		public ReadOnlyPolicy[] GetPolicies(
			AuthorizationProviderFactoryComponents authorizationProviderFactoryComponents) =>
				_authorizationPolicyFactories
					.Select(p => p(authorizationProviderFactoryComponents).Build())
					.ToArray();
	}
}
