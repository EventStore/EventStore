using EventStore.Core.Messaging;

namespace EventStore.Core.Messages {
	public static partial class AuthenticationMessage {
		[DerivedMessage(CoreMessage.Authentication)]
		public partial class AuthenticationProviderInitialized : Message<AuthenticationProviderInitialized> {
		}

		[DerivedMessage(CoreMessage.Authentication)]
		public partial class AuthenticationProviderInitializationFailed : Message<AuthenticationProviderInitializationFailed> {
		}
	}
}
