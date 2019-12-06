using System.Threading.Tasks;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Grpc.Users;
using Grpc.Core;

namespace EventStore.Core.Services.Transport.Grpc {
	public partial class Users {
		public override async Task Details(DetailsReq request, IServerStreamWriter<DetailsResp> responseStream,
			ServerCallContext context) {
			var options = request.Options;

			var user = await GetUser(_authenticationProvider, context.RequestHeaders);

			var detailsSource = new TaskCompletionSource<UserManagementMessage.UserData[]>();

			var envelope = new CallbackEnvelope(OnMessage);

			_queue.Publish(string.IsNullOrWhiteSpace(options?.LoginName)
				? (Message)new UserManagementMessage.GetAll(envelope, user)
				: new UserManagementMessage.Get(envelope, user, options.LoginName));

			var details = await detailsSource.Task;

			foreach (var detail in details) {
				await responseStream.WriteAsync(new DetailsResp {
					UserDetails = new DetailsResp.Types.UserDetails {
						Disabled = detail.Disabled,
						Groups = {detail.Groups},
						FullName = detail.FullName,
						LoginName = detail.LoginName,
						LastUpdated = detail.DateLastUpdated?.ToString()
					}
				});
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
