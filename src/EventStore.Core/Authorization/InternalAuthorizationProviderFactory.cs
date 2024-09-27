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
