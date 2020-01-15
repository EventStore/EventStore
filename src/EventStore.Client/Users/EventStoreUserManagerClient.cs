using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;

namespace EventStore.Client.Users {
	public partial class EventStoreUserManagerClient {
		private readonly Users.UsersClient _client;

		public EventStoreUserManagerClient(CallInvoker callInvoker) {
			if (callInvoker == null) {
				throw new ArgumentNullException(nameof(callInvoker));
			}

			_client = new Users.UsersClient(callInvoker);
		}

		public async Task CreateUserAsync(string loginName, string fullName, string[] groups, string password,
			UserCredentials userCredentials = null,
			CancellationToken cancellationToken = default) {
			if (loginName == null) throw new ArgumentNullException(nameof(loginName));
			if (fullName == null) throw new ArgumentNullException(nameof(fullName));
			if (groups == null) throw new ArgumentNullException(nameof(groups));
			if (password == null) throw new ArgumentNullException(nameof(password));
			if (loginName == string.Empty) throw new ArgumentOutOfRangeException(nameof(loginName));
			if (fullName == string.Empty) throw new ArgumentOutOfRangeException(nameof(fullName));
			if (password == string.Empty) throw new ArgumentOutOfRangeException(nameof(password));

			await _client.CreateAsync(new CreateReq {
				Options = new CreateReq.Types.Options {
					LoginName = loginName,
					FullName = fullName,
					Password = password,
					Groups = {groups}
				}
			}, RequestMetadata.Create(userCredentials), cancellationToken: cancellationToken);
		}

		public async Task<UserDetails> GetUserAsync(string loginName, UserCredentials userCredentials = null,
			CancellationToken cancellationToken = default) {
			if (loginName == null) throw new ArgumentNullException(nameof(loginName));
			if (loginName == string.Empty) throw new ArgumentOutOfRangeException(nameof(loginName));

			using var call = _client.Details(new DetailsReq {
				Options = new DetailsReq.Types.Options {
					LoginName = loginName
				}
			}, RequestMetadata.Create(userCredentials), cancellationToken: cancellationToken);

			await call.ResponseStream.MoveNext().ConfigureAwait(false);
			var userDetails = call.ResponseStream.Current.UserDetails;
			return ConvertUserDetails(userDetails);
		}

		private static UserDetails ConvertUserDetails(DetailsResp.Types.UserDetails userDetails) =>
			new UserDetails(userDetails.LoginName, userDetails.FullName, userDetails.Groups.ToArray(),
				userDetails.Disabled, DateTimeOffset.TryParse(userDetails.LastUpdated, out var lastUpdated)
					? lastUpdated
					: new DateTimeOffset?());

		public async Task DeleteUserAsync(string loginName, UserCredentials userCredentials = null,
			CancellationToken cancellationToken = default) {
			if (loginName == null) throw new ArgumentNullException(nameof(loginName));
			if (loginName == string.Empty) throw new ArgumentOutOfRangeException(nameof(loginName));

			await _client.DeleteAsync(new DeleteReq {
				Options = new DeleteReq.Types.Options {
					LoginName = loginName
				}
			}, RequestMetadata.Create(userCredentials), cancellationToken: cancellationToken);
		}

		public async Task EnableUserAsync(string loginName, UserCredentials userCredentials = null,
			CancellationToken cancellationToken = default) {
			if (loginName == null) throw new ArgumentNullException(nameof(loginName));
			if (loginName == string.Empty) throw new ArgumentOutOfRangeException(nameof(loginName));

			await _client.EnableAsync(new EnableReq {
				Options = new EnableReq.Types.Options {
					LoginName = loginName
				}
			}, RequestMetadata.Create(userCredentials), cancellationToken: cancellationToken);
		}

		public async Task DisableUserAsync(string loginName, UserCredentials userCredentials = null,
			CancellationToken cancellationToken = default) {
			if (loginName == null) throw new ArgumentNullException(nameof(loginName));
			if (loginName == string.Empty) throw new ArgumentOutOfRangeException(nameof(loginName));

			await _client.DisableAsync(new DisableReq {
				Options = new DisableReq.Types.Options {
					LoginName = loginName
				}
			}, RequestMetadata.Create(userCredentials), cancellationToken: cancellationToken);
		}

		public async IAsyncEnumerable<UserDetails> ListAllAsync(UserCredentials userCredentials = null,
			[EnumeratorCancellation] CancellationToken cancellationToken = default) {
			using var call = _client.Details(new DetailsReq(), RequestMetadata.Create(userCredentials),
				cancellationToken: cancellationToken);

			await foreach (var userDetail in call.ResponseStream
				.ReadAllAsync(cancellationToken)
				.Select(x => ConvertUserDetails(x.UserDetails))
				.WithCancellation(cancellationToken)
				.ConfigureAwait(false)) {
				yield return userDetail;
			}
		}

		public async Task ChangePasswordAsync(string loginName, string currentPassword, string newPassword,
			UserCredentials userCredentials = null, CancellationToken cancellationToken = default) {
			if (loginName == null) throw new ArgumentNullException(nameof(loginName));
			if (currentPassword == null) throw new ArgumentNullException(nameof(currentPassword));
			if (newPassword == null) throw new ArgumentNullException(nameof(newPassword));
			if (loginName == string.Empty) throw new ArgumentOutOfRangeException(nameof(loginName));
			if (currentPassword == string.Empty) throw new ArgumentOutOfRangeException(nameof(currentPassword));
			if (newPassword == string.Empty) throw new ArgumentOutOfRangeException(nameof(newPassword));

			await _client.ChangePasswordAsync(new ChangePasswordReq {
				Options = new ChangePasswordReq.Types.Options {
					CurrentPassword = currentPassword,
					NewPassword = newPassword,
					LoginName = loginName
				}
			}, RequestMetadata.Create(userCredentials), cancellationToken: cancellationToken);
		}

		public async Task ResetPasswordAsync(string loginName, string newPassword,
			UserCredentials userCredentials = null, CancellationToken cancellationToken = default) {
			if (loginName == null) throw new ArgumentNullException(nameof(loginName));
			if (newPassword == null) throw new ArgumentNullException(nameof(newPassword));
			if (loginName == string.Empty) throw new ArgumentOutOfRangeException(nameof(loginName));
			if (newPassword == string.Empty) throw new ArgumentOutOfRangeException(nameof(newPassword));

			await _client.ResetPasswordAsync(new ResetPasswordReq {
				Options = new ResetPasswordReq.Types.Options {
					NewPassword = newPassword,
					LoginName = loginName
				}
			}, RequestMetadata.Create(userCredentials), cancellationToken: cancellationToken);
		}
	}
}
