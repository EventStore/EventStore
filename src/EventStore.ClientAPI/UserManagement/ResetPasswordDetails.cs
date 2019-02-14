using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI.UserManagement {
	internal class ResetPasswordDetails {
		public readonly string NewPassword;

		public ResetPasswordDetails(string newPassword) {
			Ensure.NotNullOrEmpty(newPassword, "newPassword");
			NewPassword = newPassword;
		}
	}
}
