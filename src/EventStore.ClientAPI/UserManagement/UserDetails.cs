using System;
using System.Linq;

namespace EventStore.ClientAPI.UserManagement {
	/// <summary>
	/// Provides the details for a user.
	/// </summary>
	public sealed class UserDetails {
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
		/// List of hypermedia links describing actions allowed on user resource.
		/// </summary>
		public readonly RelLink[] Links;

		/// <summary>
		/// create a new <see cref="UserDetails"/> class.
		/// </summary>
		/// <param name="loginName">The login name of the user.</param>
		/// <param name="fullName">The users full name.</param>
		/// <param name="groups">The groups this user is a member if.</param>
		/// <param name="disabled">Is this user disabled or not.</param>
		/// <param name="dateLastUpdated">The datt/time this user was last updated in UTC format.</param>
		/// <param name="links">List of hypermedia links describing actions allowed on user resource.</param>
		public UserDetails(
			string loginName, string fullName, string[] groups, bool disabled, DateTimeOffset? dateLastUpdated,
			RelLink[] links) {
			LoginName = loginName;
			FullName = fullName;
			Groups = groups;
			Disabled = disabled;
			DateLastUpdated = dateLastUpdated;
			Links = links;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="rel"></param>
		/// <returns></returns>
		public string GetRelLink(string rel) {
			if (Links == null) throw new Exception();

			var link = Links.SingleOrDefault(x => string.Equals(x.Rel, rel, StringComparison.OrdinalIgnoreCase));

			if (link == null)
				throw new Exception();

			return link.Href;
		}
	}
}
