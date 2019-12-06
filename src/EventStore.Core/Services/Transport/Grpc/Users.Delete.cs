using System.Threading.Tasks;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Grpc.Users;
using Grpc.Core;

namespace EventStore.Core.Services.Transport.Grpc {
	partial class Users {
		public override async Task<DeleteResp> Delete(DeleteReq request, ServerCallContext context) {
			var options = request.Options;

			var user = await GetUser(_authenticationProvider, context.RequestHeaders);

			var deleteSource = new TaskCompletionSource<bool>();

			var envelope = new CallbackEnvelope(OnMessage);

			_queue.Publish(new UserManagementMessage.Delete(envelope, user, options.LoginName));

			await deleteSource.Task;

			return new DeleteResp();

			void OnMessage(Message message) {
				if (HandleErrors(options.LoginName, message, deleteSource)) return;

				deleteSource.TrySetResult(true);
			}
		}
	}
}
