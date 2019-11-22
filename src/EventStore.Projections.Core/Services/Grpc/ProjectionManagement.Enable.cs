using System.Security.Principal;
using System.Threading.Tasks;
using EventStore.Core;
using EventStore.Core.Messaging;
using EventStore.Grpc.Projections;
using EventStore.Projections.Core.Messages;
using Grpc.Core;

namespace EventStore.Projections.Core.Services.Grpc {
	public partial class ProjectionManagement {
		public override async Task<EnableResp> Enable(EnableReq request, ServerCallContext context) {
			var enableSource = new TaskCompletionSource<bool>();

			var options = request.Options;

			var user = await GetUser(_authenticationProvider, context.RequestHeaders);

			var name = options.Name;
			var runAs = new ProjectionManagementMessage.RunAs(user);

			var envelope = new CallbackEnvelope(OnMessage);

			_queue.Publish(new ProjectionManagementMessage.Command.Enable(envelope, name, runAs));

			await enableSource.Task;

			return new EnableResp();

			void OnMessage(Message message) {
				if (!(message is ProjectionManagementMessage.Updated)) {
					enableSource.TrySetException(UnknownMessage<ProjectionManagementMessage.Updated>(message));
					return;
				}

				enableSource.TrySetResult(true);
			}
		}
	}
}
