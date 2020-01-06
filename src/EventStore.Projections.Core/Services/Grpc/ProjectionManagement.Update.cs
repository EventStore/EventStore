using System;
using System.Security.Principal;
using System.Threading.Tasks;
using EventStore.Core;
using EventStore.Core.Messaging;
using EventStore.Grpc.Projections;
using EventStore.Projections.Core.Messages;
using Grpc.Core;
using static EventStore.Grpc.Projections.UpdateReq.Types.Options;

namespace EventStore.Projections.Core.Services.Grpc {
	public partial class ProjectionManagement {
		public override async Task<UpdateResp> Update(UpdateReq request, ServerCallContext context) {
			var updatedSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			var options = request.Options;

			var user = await GetUser(_authenticationProvider, context.RequestHeaders).ConfigureAwait(false);

			const string handlerType = "JS";
			var name = options.Name;
			var query = options.Query;
			bool? emitEnabled = (options.EmitOptionCase, options.EmitEnabled) switch {
				(EmitOptionOneofCase.EmitEnabled, true) => true,
				(EmitOptionOneofCase.EmitEnabled, false) => false,
				(EmitOptionOneofCase.NoEmitOptions, _) => default,
				_ => throw new InvalidOperationException()
			};
			var runAs = new ProjectionManagementMessage.RunAs(user);

			var envelope = new CallbackEnvelope(OnMessage);
			_queue.Publish(
				new ProjectionManagementMessage.Command.UpdateQuery(envelope, name, runAs, handlerType, query,
					emitEnabled));

			await updatedSource.Task.ConfigureAwait(false);

			return new UpdateResp();

			void OnMessage(Message message) {
				if (!(message is ProjectionManagementMessage.Updated)) {
					updatedSource.TrySetException(UnknownMessage<ProjectionManagementMessage.Updated>(message));
					return;
				}

				updatedSource.TrySetResult(true);
			}
		}
	}
}
