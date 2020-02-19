namespace EventStore.Core.Services.Transport.Http {
	internal class OptionalAuthorizationLevel {
		private readonly AuthorizationLevel _authorizationLevel;

		public OptionalAuthorizationLevel(AuthorizationLevel authorizationLevel) {
			_authorizationLevel = authorizationLevel;
		}

		public static implicit operator AuthorizationLevel(OptionalAuthorizationLevel level) =>
			level._authorizationLevel;
	}
}
