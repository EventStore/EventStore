namespace EventStore.Core.Data {
	public class UserData {
		public readonly string LoginName;
		public readonly string FullName;
		public readonly string Salt;
		public readonly string Hash;
		public readonly bool Disabled;
		public readonly string[] Groups;

		public UserData(string loginName, string fullName, string[] groups, string hash, string salt, bool disabled) {
			LoginName = loginName;
			FullName = fullName;
			Groups = groups;
			Salt = salt;
			Hash = hash;
			Disabled = disabled;
		}

		public UserData SetFullName(string fullName) {
			return new UserData(LoginName, fullName, Groups, Hash, Salt, Disabled);
		}

		public UserData SetGroups(string[] groups) {
			return new UserData(LoginName, FullName, groups, Hash, Salt, Disabled);
		}

		public UserData SetPassword(string hash, string salt) {
			return new UserData(LoginName, FullName, Groups, hash, salt, Disabled);
		}

		public UserData SetEnabled() {
			return new UserData(LoginName, FullName, Groups, Hash, Salt, disabled: false);
		}

		public UserData SetDisabled() {
			return new UserData(LoginName, FullName, Groups, Hash, Salt, disabled: true);
		}
	}
}
