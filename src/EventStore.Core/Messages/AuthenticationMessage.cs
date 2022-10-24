using EventStore.Core.Messaging;

namespace EventStore.Core.Messages
{
	public static partial class AuthenticationMessage {
		//qq consider all these names
		[StatsGroup("authentication")]
		public enum MessageType {
			None = 0,
			AuthenticationProviderInitialized = 1,
			AuthenticationProviderInitializationFailed = 2,
		}

		[StatsMessage(MessageType.AuthenticationProviderInitialized)]
		public partial class AuthenticationProviderInitialized : Message {
		}

		[StatsMessage(MessageType.AuthenticationProviderInitializationFailed)]
		public partial class AuthenticationProviderInitializationFailed : Message {
		}
	}
}
