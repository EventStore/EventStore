using System;

namespace EventStore.Grpc {
	public class UserCredentials {
		public readonly string Username;
		public readonly string Password;

		public UserCredentials(string username, string password) {
			if (username == null) {
				throw new ArgumentNullException(nameof(username));
			}

			if (password == null) {
				throw new ArgumentNullException(nameof(password));
			}

			Username = username;
			Password = password;
		}
	}
}
