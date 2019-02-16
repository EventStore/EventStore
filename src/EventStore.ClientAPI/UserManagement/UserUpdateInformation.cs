using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI.UserManagement {
	internal class UserUpdateInformation {
		public readonly string FullName;

		public readonly string[] Groups;

		public UserUpdateInformation(string fullName, string[] groups) {
			Ensure.NotNullOrEmpty(fullName, "fullName");
			Ensure.NotNull(groups, "fullName");
			FullName = fullName;
			Groups = groups;
		}
	}
}
