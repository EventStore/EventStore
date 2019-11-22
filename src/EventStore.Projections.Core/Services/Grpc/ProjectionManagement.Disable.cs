using System.Security.Principal;
using System.Threading.Tasks;
using EventStore.Core;
using EventStore.Core.Messaging;
using EventStore.Grpc.Projections;
using EventStore.Projections.Core.Messages;
using Grpc.Core;

namespace EventStore.Projections.Core.Services.Grpc {
	public partial class ProjectionManagement {
		public override async Task<DisableResp> Disable(DisableReq request, ServerCallContext context) {
			var disableSource = new TaskCompletionSource<bool>();

			var options = request.Options;

			var user = await GetUser(_authenticationProvider, context.RequestHeaders);

			var name = options.Name;
			var runAs = new ProjectionManagementMessage.RunAs(user);

			var envelope = new CallbackEnvelope(OnMessage);

			_queue.Publish(options.WriteCheckpoint
				? (Message)new ProjectionManagementMessage.Command.Abort(envelope, name, runAs)
				: new ProjectionManagementMessage.Command.Disable(envelope, name, runAs));

			await disableSource.Task;

			return new DisableResp();

			void OnMessage(Message message) {
				if (!(message is ProjectionManagementMessage.Updated)) {
					disableSource.TrySetException(UnknownMessage<ProjectionManagementMessage.Updated>(message));
					return;
				}

				disableSource.TrySetResult(true);
			}
		}
	}
}
