using System.Security.Claims;
using System.Threading.Tasks;
using EventStore.Core;
using EventStore.Core.Messaging;
using EventStore.Client.Projections;
using EventStore.Core.Authorization;
using EventStore.Projections.Core.Messages;
using Grpc.Core;

namespace EventStore.Projections.Core.Services.Grpc {
	public partial class ProjectionManagement {
		private static readonly Operation DisableOperation = new Operation(Operations.Projections.Disable);
		public override async Task<DisableResp> Disable(DisableReq request, ServerCallContext context) {
			var disableSource = new TaskCompletionSource<bool>();

			var options = request.Options;

			var user = context.GetHttpContext().User;
			if (!await _authorizationProvider.CheckAccessAsync(user, DisableOperation, context.CancellationToken)
				.ConfigureAwait(false)) {
				throw AccessDenied();
			}
			var name = options.Name;
			var runAs = new ProjectionManagementMessage.RunAs(user);

			var envelope = new CallbackEnvelope(OnMessage);

			_queue.Publish(options.WriteCheckpoint
				? (Message)new ProjectionManagementMessage.Command.Abort(envelope, name, runAs)
				: new ProjectionManagementMessage.Command.Disable(envelope, name, runAs));

			await disableSource.Task.ConfigureAwait(false);

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
