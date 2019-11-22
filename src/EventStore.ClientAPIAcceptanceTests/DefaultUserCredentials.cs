using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI.Tests {
	internal static class DefaultUserCredentials {
		public static readonly UserCredentials Admin = new UserCredentials("admin", "changeit");
	}
}
