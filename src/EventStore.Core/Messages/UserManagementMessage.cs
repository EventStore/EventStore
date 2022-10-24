using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Http.Controllers;

namespace EventStore.Core.Messages {
	public static partial class UserManagementMessage {
		[StatsGroup("user-management")]
		public enum MessageType {
			None = 0,
			RequestMessage = 1,
			ResponseMessage = 2,
			UserManagementRequestMessage = 3,
			Create = 4,
			Update = 5,
			Disable = 6,
			Enable = 7,
			Delete = 8,
			ResetPassword = 9,
			ChangePassword = 10,
			GetAll = 11,
			Get = 12,
			UpdateResult = 13,
			UserDetailsResultHttpFormatted = 14,
			AllUserDetailsResultHttpFormatted = 15,
			UserDetailsResult = 16,
			AllUserDetailsResult = 17,
		}

		[StatsMessage(MessageType.RequestMessage)]
		public partial class RequestMessage : Message {
			public readonly IEnvelope Envelope;
			public readonly ClaimsPrincipal Principal;

			public RequestMessage(IEnvelope envelope, ClaimsPrincipal principal) {
				Envelope = envelope;
				Principal = principal;
			}
		}

		[StatsMessage(MessageType.ResponseMessage)]
		public partial class ResponseMessage : Message {
			public readonly bool Success;
			public readonly Error Error;

			public ResponseMessage(bool success, Error error) {
				Success = success;
				Error = error;
			}
		}

		[StatsMessage(MessageType.UserManagementRequestMessage)]
		public partial class UserManagementRequestMessage : RequestMessage {
			public readonly string LoginName;

			protected UserManagementRequestMessage(IEnvelope envelope, ClaimsPrincipal principal, string loginName)
				: base(envelope, principal) {
				LoginName = loginName;
			}
		}

		[StatsMessage(MessageType.Create)]
		public sealed partial class Create : UserManagementRequestMessage {
			public readonly string FullName;
			public readonly string[] Groups;
			public readonly string Password;

			public Create(
				IEnvelope envelope, ClaimsPrincipal principal, string loginName, string fullName, string[] groups,
				string password)
				: base(envelope, principal, loginName) {
				FullName = fullName;
				Groups = groups;
				Password = password;
			}
		}

		[StatsMessage(MessageType.Update)]
		public sealed partial class Update : UserManagementRequestMessage {
			public readonly string FullName;
			public readonly string[] Groups;

			public Update(IEnvelope envelope, ClaimsPrincipal principal, string loginName, string fullName, string[] groups)
				: base(envelope, principal, loginName) {
				FullName = fullName;
				Groups = groups;
			}
		}

		[StatsMessage(MessageType.Disable)]
		public sealed partial class Disable : UserManagementRequestMessage {
			public Disable(IEnvelope envelope, ClaimsPrincipal principal, string loginName)
				: base(envelope, principal, loginName) {
			}
		}

		[StatsMessage(MessageType.Enable)]
		public sealed partial class Enable : UserManagementRequestMessage {
			public Enable(IEnvelope envelope, ClaimsPrincipal principal, string loginName)
				: base(envelope, principal, loginName) {
			}
		}

		[StatsMessage(MessageType.Delete)]
		public sealed partial class Delete : UserManagementRequestMessage {
			public Delete(IEnvelope envelope, ClaimsPrincipal principal, string loginName)
				: base(envelope, principal, loginName) {
			}
		}

		[StatsMessage(MessageType.ResetPassword)]
		public sealed partial class ResetPassword : UserManagementRequestMessage {
			public readonly string NewPassword;

			public ResetPassword(IEnvelope envelope, ClaimsPrincipal principal, string loginName, string newPassword)
				: base(envelope, principal, loginName) {
				NewPassword = newPassword;
			}
		}

		[StatsMessage(MessageType.ChangePassword)]
		public sealed partial class ChangePassword : UserManagementRequestMessage {
			public readonly string CurrentPassword;
			public readonly string NewPassword;

			public ChangePassword(
				IEnvelope envelope, ClaimsPrincipal principal, string loginName, string currentPassword, string newPassword)
				: base(envelope, principal, loginName) {
				CurrentPassword = currentPassword;
				NewPassword = newPassword;
			}
		}

		[StatsMessage(MessageType.GetAll)]
		public sealed partial class GetAll : RequestMessage {
			public GetAll(IEnvelope envelope, ClaimsPrincipal principal)
				: base(envelope, principal) {
			}
		}

		[StatsMessage(MessageType.Get)]
		public sealed partial class Get : UserManagementRequestMessage {
			public Get(IEnvelope envelope, ClaimsPrincipal principal, string loginName)
				: base(envelope, principal, loginName) {
			}
		}

		public enum Error {
			Success,
			NotFound,
			Conflict,
			Error,
			TryAgain,
			Unauthorized
		}

		public sealed class UserData {
			public readonly string LoginName;
			public readonly string FullName;
			public readonly string[] Groups;
			public readonly DateTimeOffset? DateLastUpdated;
			public readonly bool Disabled;

			public UserData(
				string loginName, string fullName, string[] groups, bool disabled, DateTimeOffset? dateLastUpdated) {
				LoginName = loginName;
				FullName = fullName;
				Groups = groups;
				Disabled = disabled;
				DateLastUpdated = dateLastUpdated;
			}

			internal UserData() {
			}
		}

		[StatsMessage(MessageType.UpdateResult)]
		public sealed partial class UpdateResult : ResponseMessage {
			public readonly string LoginName;

			public UpdateResult(string loginName)
				: base(true, Error.Success) {
				LoginName = loginName;
			}

			public UpdateResult(string loginName, Error error)
				: base(false, error) {
				LoginName = loginName;
			}
		}

		public class UserDataHttpFormated {
			public readonly string LoginName;
			public readonly string FullName;
			public readonly string[] Groups;
			public readonly DateTimeOffset? DateLastUpdated;
			public readonly bool Disabled;
			public readonly List<RelLink> Links;

			public UserDataHttpFormated(UserData userData, Func<string, string> makeAbsoluteUrl) {
				LoginName = userData.LoginName;
				FullName = userData.FullName;
				Groups = userData.Groups;
				Disabled = userData.Disabled;

				Links = new List<RelLink>();
				var userLocalUrl = "/users/" + userData.LoginName;
				Links.Add(new RelLink(makeAbsoluteUrl(userLocalUrl + "/command/reset-password"), "reset-password"));
				Links.Add(new RelLink(makeAbsoluteUrl(userLocalUrl + "/command/change-password"), "change-password"));
				Links.Add(new RelLink(makeAbsoluteUrl(userLocalUrl), "edit"));
				Links.Add(new RelLink(makeAbsoluteUrl(userLocalUrl), "delete"));

				Links.Add(userData.Disabled
					? new RelLink(makeAbsoluteUrl(userLocalUrl + "/command/enable"), "enable")
					: new RelLink(makeAbsoluteUrl(userLocalUrl + "/command/disable"), "disable"));
			}
		}


		[StatsMessage(MessageType.UserDetailsResultHttpFormatted)]
		public partial class UserDetailsResultHttpFormatted : ResponseMessage {
			public readonly UserDataHttpFormated Data;

			public UserDetailsResultHttpFormatted(UserDetailsResult msg, Func<string, string> makeAbsoluteUrl) :
				base(msg.Success, msg.Error) {
				if (msg.Data != null)
					Data = new UserDataHttpFormated(msg.Data, makeAbsoluteUrl);
			}
		}

		[StatsMessage(MessageType.AllUserDetailsResultHttpFormatted)]
		public partial class AllUserDetailsResultHttpFormatted : ResponseMessage {
			public readonly UserDataHttpFormated[] Data;

			public AllUserDetailsResultHttpFormatted(AllUserDetailsResult msg, Func<string, string> makeAbsoluteUrl) :
				base(msg.Success, msg.Error) {
				Data = msg.Data.Select(user => new UserDataHttpFormated(user, makeAbsoluteUrl)).ToArray();
			}
		}


		[StatsMessage(MessageType.UserDetailsResult)]
		public sealed partial class UserDetailsResult : ResponseMessage {
			public readonly UserData Data;

			public UserDetailsResult(UserData data)
				: base(true, Error.Success) {
				Data = data;
			}

			public UserDetailsResult(Error error)
				: base(false, error) {
				Data = null;
			}
		}

		[StatsMessage(MessageType.AllUserDetailsResult)]
		public sealed partial class AllUserDetailsResult : ResponseMessage {
			public readonly UserData[] Data;

			internal AllUserDetailsResult()
				: base(true, Error.Success) {
			}

			public AllUserDetailsResult(UserData[] data)
				: base(true, Error.Success) {
				Data = data;
			}

			public AllUserDetailsResult(Error error)
				: base(false, error) {
				Data = null;
			}
		}
	}
}
