using System.Linq;
using System.Threading.Tasks;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Grpc.Users;
using Grpc.Core;

namespace EventStore.Core.Services.Transport.Grpc {
	partial class Users {
		public override async Task<UpdateResp> Update(UpdateReq request, ServerCallContext context) {
			var options = request.Options;

			var user = await GetUser(_authenticationProvider, context.RequestHeaders);

			var updateSource = new TaskCompletionSource<bool>();

			var envelope = new CallbackEnvelope(OnMessage);

			_queue.Publish(new UserManagementMessage.Update(envelope, user, options.LoginName, options.FullName,
				options.Groups.ToArray()));

			await updateSource.Task;

			return new UpdateResp();

			void OnMessage(Message message) {
				if (HandleErrors(options.LoginName, message, updateSource)) return;

				updateSource.TrySetResult(true);
			}
		}
	}
}
