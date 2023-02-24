using EventStore.Core.Messaging;

namespace EventStore.Core.Messages {
	public static partial class InternalAuthenticationProviderMessages {
		[DerivedMessage(CoreMessage.Authentication)]
		public sealed partial class ResetPasswordCache : Message {
			public readonly string LoginName;

			public ResetPasswordCache(string loginName) {
				LoginName = loginName;
			}
		}
	}
}
