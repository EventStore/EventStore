namespace EventStore.Core.Authentication {
	public static class AuthenticationMessage {
		public abstract class AuthenticationProviderMessage {
		}

		public sealed class AuthenticationProviderInitialized : AuthenticationProviderMessage {
		}
	}
}
