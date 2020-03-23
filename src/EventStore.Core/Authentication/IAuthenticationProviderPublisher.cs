namespace EventStore.Core.Authentication {
	public interface IAuthenticationProviderPublisher {
		void Publish(AuthenticationMessage.AuthenticationProviderMessage message);
	}
}
