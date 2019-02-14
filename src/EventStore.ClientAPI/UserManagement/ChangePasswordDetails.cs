using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI.UserManagement {
	internal class ChangePasswordDetails {
		public readonly string CurrentPassword;

		public readonly string NewPassword;

		public ChangePasswordDetails(string currentPassword, string newPassword) {
			Ensure.NotNullOrEmpty(currentPassword, "currentPassword");
			Ensure.NotNullOrEmpty(newPassword, "newPassword");
			CurrentPassword = currentPassword;
			NewPassword = newPassword;
		}
	}
}
