using System;
using System.Linq;

namespace EventStore.Client.Users {
	/// <summary>
	/// Provides the details for a user.
	/// </summary>
	public struct UserDetails : IEquatable<UserDetails> {
		/// <summary>
		/// The users login name.
		/// </summary>
		public readonly string LoginName;

		/// <summary>
		/// The full name of the user.
		/// </summary>
		public readonly string FullName;

		/// <summary>
		/// The groups the user is a member of.
		/// </summary>
		public readonly string[] Groups;

		/// <summary>
		/// The date/time the user was updated in UTC format.
		/// </summary>
		public readonly DateTimeOffset? DateLastUpdated;

		/// <summary>
		/// Whether the user disable or not.
		/// </summary>
		public readonly bool Disabled;

		/// <summary>
		/// create a new <see cref="UserDetails"/> class.
		/// </summary>
		/// <param name="loginName">The login name of the user.</param>
		/// <param name="fullName">The users full name.</param>
		/// <param name="groups">The groups this user is a member if.</param>
		/// <param name="disabled">Is this user disabled or not.</param>
		/// <param name="dateLastUpdated">The datt/time this user was last updated in UTC format.</param>
		public UserDetails(
			string loginName, string fullName, string[] groups, bool disabled, DateTimeOffset? dateLastUpdated) {
			if (loginName == null) {
				throw new ArgumentNullException(nameof(loginName));
			}
			if (fullName == null) {
				throw new ArgumentNullException(nameof(fullName));
			}
			if (groups == null) {
				throw new ArgumentNullException(nameof(groups));
			}

			LoginName = loginName;
			FullName = fullName;
			Groups = groups;
			Disabled = disabled;
			DateLastUpdated = dateLastUpdated;
		}

		public bool Equals(UserDetails other) =>
			LoginName == other.LoginName && FullName == other.FullName && Groups.SequenceEqual(other.Groups) &&
			Nullable.Equals(DateLastUpdated, other.DateLastUpdated) && Disabled == other.Disabled;

		public override bool Equals(object obj) => obj is UserDetails other && Equals(other);

		public override int GetHashCode() => HashCode.Hash.Combine(LoginName).Combine(FullName).Combine(Groups)
			.Combine(Disabled).Combine(DateLastUpdated);

		public static bool operator ==(UserDetails left, UserDetails right) => left.Equals(right);
		public static bool operator !=(UserDetails left, UserDetails right) => !left.Equals(right);
	}
}
