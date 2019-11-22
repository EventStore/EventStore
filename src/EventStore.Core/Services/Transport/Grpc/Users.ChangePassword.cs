using System.Threading.Tasks;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Grpc.Users;
using Grpc.Core;

namespace EventStore.Core.Services.Transport.Grpc {
	public partial class Users {
		public override async Task<ChangePasswordResp> ChangePassword(ChangePasswordReq request,
			ServerCallContext context) {
			var options = request.Options;

			var user = await GetUser(_authenticationProvider, context.RequestHeaders);

			var changePasswordSource = new TaskCompletionSource<bool>();

			var envelope = new CallbackEnvelope(OnMessage);

			_queue.Publish(new UserManagementMessage.ChangePassword(envelope, user, options.LoginName,
				options.CurrentPassword,
				options.NewPassword));

			await changePasswordSource.Task;

			return new ChangePasswordResp();

			void OnMessage(Message message) {
				if (HandleErrors(options.LoginName, message, changePasswordSource)) return;

				changePasswordSource.TrySetResult(true);
			}
		}
	}
}
