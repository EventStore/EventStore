using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPIAcceptanceTests {
	internal static class DefaultUserCredentials {
		public static readonly UserCredentials Admin = new UserCredentials("admin", "changeit");
	}
}
