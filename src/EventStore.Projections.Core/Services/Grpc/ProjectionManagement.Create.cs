using System;
using System.Threading.Tasks;
using EventStore.Core.Messaging;
using EventStore.Client.Projections;
using EventStore.Plugins.Authorization;
using EventStore.Projections.Core.Messages;
using Grpc.Core;
using static EventStore.Client.Projections.CreateReq.Types.Options;

namespace EventStore.Projections.Core.Services.Grpc {
	internal partial class ProjectionManagement {
		private static readonly Operation CreateOperation = new Operation(Operations.Projections.Create);

		public override async Task<CreateResp> Create(CreateReq request, ServerCallContext context) {
			var createdSource = new TaskCompletionSource<bool>();
			var options = request.Options;

			var user = context.GetHttpContext().User;
			if (!await _authorizationProvider.CheckAccessAsync(user, CreateOperation, context.CancellationToken)
				.ConfigureAwait(false)) {
				throw AccessDenied();
			}
			const string handlerType = "JS";
			var name = options.ModeCase switch {
				ModeOneofCase.Continuous => options.Continuous.Name,
				ModeOneofCase.Transient => options.Transient.Name,
				ModeOneofCase.OneTime => Guid.NewGuid().ToString("D"),
				_ => throw new InvalidOperationException()
			};
			var projectionMode = options.ModeCase switch {
				ModeOneofCase.Continuous => ProjectionMode.Continuous,
				ModeOneofCase.Transient => ProjectionMode.Transient,
				ModeOneofCase.OneTime => ProjectionMode.OneTime,
				_ => throw new InvalidOperationException()
			};
			var emitEnabled = options.ModeCase switch {
				ModeOneofCase.Continuous => options.Continuous.TrackEmittedStreams,
				_ => false
			};
			var checkpointsEnables = options.ModeCase switch {
				ModeOneofCase.Continuous => true,
				ModeOneofCase.OneTime => false,
				ModeOneofCase.Transient => false,
				_ => throw new InvalidOperationException()
			};
			var enabled = true;
			var trackEmittedStreams = (options.ModeCase, emitEnabled) switch {
				(ModeOneofCase.Continuous, false) => true,
				_ => false
			};
			var runAs = new ProjectionManagementMessage.RunAs(user);

			var envelope = new CallbackEnvelope(OnMessage);

			_queue.Publish(new ProjectionManagementMessage.Command.Post(envelope, projectionMode, name, runAs,
				handlerType, options.Query, enabled, checkpointsEnables, emitEnabled, trackEmittedStreams, true));

			await createdSource.Task.ConfigureAwait(false);

			return new CreateResp();

			void OnMessage(Message message) {
				if (!(message is ProjectionManagementMessage.Updated)) {
					createdSource.TrySetException(UnknownMessage<ProjectionManagementMessage.Updated>(message));
					return;
				}

				createdSource.TrySetResult(true);
			}
		}
	}
}
