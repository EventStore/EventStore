using System.Threading.Tasks;
using EventStore.Core.Messaging;
using EventStore.Client.Shared;
using EventStore.Plugins.Authorization;
using EventStore.Projections.Core.Messages;
using Grpc.Core;

namespace EventStore.Projections.Core.Services.Grpc {
	public partial class ProjectionManagement {
		private static readonly Operation RestartOperation = new Operation(Operations.Projections.Restart);

		public override async Task<Empty> RestartSubsystem(Empty empty, ServerCallContext context) {
			var restart = new TaskCompletionSource<bool>();
			var envelope = new CallbackEnvelope(OnMessage);

			var user = context.GetHttpContext().User;
			if (!await _authorizationProvider.CheckAccessAsync(user, RestartOperation, context.CancellationToken)
				.ConfigureAwait(false)) {
				throw AccessDenied();
			}

			_queue.Publish(new ProjectionSubsystemMessage.RestartSubsystem(envelope));

			await restart.Task.ConfigureAwait(false);
			return new Empty();

			void OnMessage(Message message) {
				switch (message) {
					case ProjectionSubsystemMessage.SubsystemRestarting _:
						restart.TrySetResult(true);
						break;
					case ProjectionSubsystemMessage.InvalidSubsystemRestart fail:
						restart.TrySetException(InvalidSubsystemRestart(fail.SubsystemState));
						break;
					default:
						restart.TrySetException(
							UnknownMessage<ProjectionSubsystemMessage.SubsystemRestarting>(message));
						break;
				}
			}
		}
	}
}
