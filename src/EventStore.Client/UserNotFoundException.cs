using System;

namespace EventStore.Client {
	public class UserNotFoundException : Exception {
		public string LoginName { get; }

		public UserNotFoundException(string loginName, Exception exception = default)
			: base($"User '{loginName}' was not found.", exception) {
			LoginName = loginName;
		}
	}
}
