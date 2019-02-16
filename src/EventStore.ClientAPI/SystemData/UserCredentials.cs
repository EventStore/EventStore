using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI.SystemData {
	/// <summary>
	/// A username/password pair used for authentication and
	/// authorization to perform operations over an <see cref="IEventStoreConnection"/>.
	/// </summary>
	public class UserCredentials {
		/// <summary>
		/// The username
		/// </summary>
		public readonly string Username;

		/// <summary>
		/// The password
		/// </summary>
		public readonly string Password;

		/// <summary>
		/// Constructs a new <see cref="UserCredentials"/>.
		/// </summary>
		/// <param name="username"></param>
		/// <param name="password"></param>
		public UserCredentials(string username, string password) {
			Ensure.NotNull(username, "username");
			Ensure.NotNull(password, "password");

			Username = username;
			Password = password;
		}
	}
}
