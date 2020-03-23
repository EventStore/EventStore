namespace EventStore.Core.Authentication {
	public interface IAuthenticationProviderFactory {
		IAuthenticationProvider BuildAuthenticationProvider(IAuthenticationProviderPublisher publisher,
			bool logFailedAuthenticationAttempts);
	}
}
