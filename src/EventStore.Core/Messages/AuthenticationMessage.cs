using EventStore.Core.Messaging;

namespace EventStore.Core.Messages {
	public static partial class AuthenticationMessage {
		[DerivedMessage]
		public partial class AuthenticationProviderInitialized : Message {
		}

		[DerivedMessage]
		public partial class AuthenticationProviderInitializationFailed : Message {
		}
	}
}
