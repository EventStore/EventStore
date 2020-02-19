using System.Threading.Tasks;
using EventStore.Client;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Client.Users;
using Grpc.Core;

namespace EventStore.Core.Services.Transport.Grpc {
	public partial class Users {
		public override async Task Details(DetailsReq request, IServerStreamWriter<DetailsResp> responseStream,
			ServerCallContext context) {
			var options = request.Options;

			var user = context.GetHttpContext().User;

			var detailsSource = new TaskCompletionSource<UserManagementMessage.UserData[]>();

			var envelope = new CallbackEnvelope(OnMessage);

			_queue.Publish(string.IsNullOrWhiteSpace(options?.LoginName)
				? (Message)new UserManagementMessage.GetAll(envelope, user)
				: new UserManagementMessage.Get(envelope, user, options.LoginName));

			var details = await detailsSource.Task.ConfigureAwait(false);

			foreach (var detail in details) {
				await responseStream.WriteAsync(new DetailsResp {
					UserDetails = new DetailsResp.Types.UserDetails {
						Disabled = detail.Disabled,
						Groups = {detail.Groups},
						FullName = detail.FullName,
						LoginName = detail.LoginName,
						LastUpdated = detail.DateLastUpdated.HasValue
							? new DetailsResp.Types.UserDetails.Types.DateTime
								{TicksSinceEpoch = detail.DateLastUpdated.Value.UtcDateTime.ToTicksSinceEpoch()}
							: null
					}
				}).ConfigureAwait(false);
			}

			void OnMessage(Message message) {
				if (HandleErrors(options?.LoginName, message, detailsSource)) return;

				switch (message) {
					case UserManagementMessage.UserDetailsResult userDetails:
						detailsSource.TrySetResult(new[] {userDetails.Data});
						break;
					case UserManagementMessage.AllUserDetailsResult allUserDetails:
						detailsSource.TrySetResult(allUserDetails.Data);
						break;
					default:
						detailsSource.TrySetException(RpcExceptions.UnknownError(1));
						break;
				}
			}
		}
	}
}
