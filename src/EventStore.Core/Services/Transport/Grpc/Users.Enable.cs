using System.Threading.Tasks;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Grpc.Users;
using Grpc.Core;

namespace EventStore.Core.Services.Transport.Grpc {
	public partial class Users {
		public override async Task<EnableResp> Enable(EnableReq request, ServerCallContext context) {
			var options = request.Options;

			var user = await GetUser(_authenticationProvider, context.RequestHeaders);

			var enableSource = new TaskCompletionSource<bool>();

			var envelope = new CallbackEnvelope(OnMessage);

			_queue.Publish(new UserManagementMessage.Enable(envelope, user, options.LoginName));

			await enableSource.Task;

			return new EnableResp();

			void OnMessage(Message message) {
				if (HandleErrors(options.LoginName, message, enableSource)) return;

				enableSource.TrySetResult(true);
			}
		}
	}
}
