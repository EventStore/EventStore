namespace EventStore.Core.Authentication {
	public interface IAuthenticationProvider {
		void Authenticate(AuthenticationRequest authenticationRequest);
	}
}
