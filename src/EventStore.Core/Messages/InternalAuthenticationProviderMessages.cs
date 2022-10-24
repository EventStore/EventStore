using EventStore.Core.Messaging;

namespace EventStore.Core.Messages {
	public static partial class InternalAuthenticationProviderMessages {
		[StatsGroup("internal-authentication-provider")]
		public enum MessageType {
			None = 0,
			ResetPasswordCache = 1,
		}

		[StatsMessage(MessageType.ResetPasswordCache)]
		public sealed partial class ResetPasswordCache : Message {
			public readonly string LoginName;

			public ResetPasswordCache(string loginName) {
				LoginName = loginName;
			}
		}
	}
}
