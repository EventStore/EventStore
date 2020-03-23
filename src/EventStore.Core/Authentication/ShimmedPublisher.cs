using EventStore.Core.Authentication.InternalAuthentication;
using EventStore.Core.Bus;

namespace EventStore.Core.Authentication {
	public class ShimmedPublisher : IAuthenticationProviderPublisher {
		private readonly IPublisher _mainQueue;

		public ShimmedPublisher(IPublisher mainQueue) {
			_mainQueue = mainQueue;
		}

		public void Publish(AuthenticationMessage.AuthenticationProviderMessage message) {
			if (message is AuthenticationMessage.AuthenticationProviderInitialized) {
				_mainQueue.Publish(new InternalAuthenticationMessage.AuthenticationProviderInitialized());
			}
		}
	}
}
