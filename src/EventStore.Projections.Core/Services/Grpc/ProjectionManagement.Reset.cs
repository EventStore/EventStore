using System.Security.Principal;
using System.Threading.Tasks;
using EventStore.Core;
using EventStore.Core.Messaging;
using EventStore.Grpc.Projections;
using EventStore.Projections.Core.Messages;
using Grpc.Core;

namespace EventStore.Projections.Core.Services.Grpc {
	public partial class ProjectionManagement {
		public override async Task<ResetResp> Reset(ResetReq request, ServerCallContext context) {
			var resetSource = new TaskCompletionSource<bool>();

			var options = request.Options;

			var user = await GetUser(_authenticationProvider, context.RequestHeaders);

			var name = options.Name;
			var runAs = new ProjectionManagementMessage.RunAs(user);

			var envelope = new CallbackEnvelope(OnMessage);

			_queue.Publish(new ProjectionManagementMessage.Command.Reset(envelope, name, runAs));

			await resetSource.Task;

			return new ResetResp();

			void OnMessage(Message message) {
				if (!(message is ProjectionManagementMessage.Updated)) {
					resetSource.TrySetException(UnknownMessage<ProjectionManagementMessage.Updated>(message));
					return;
				}

				resetSource.TrySetResult(true);
			}
		}
	}
}
