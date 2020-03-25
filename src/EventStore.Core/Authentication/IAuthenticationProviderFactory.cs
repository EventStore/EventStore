namespace EventStore.Core.Authentication {
	public interface IAuthenticationProviderFactory {
		IAuthenticationProvider Build(bool logFailedAuthenticationAttempts);
	}
}
