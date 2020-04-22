using System;
using System.Threading.Tasks;
using EventStore.Client.Operations;
using EventStore.Client.Shared;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Plugins.Authorization;
using Grpc.Core;

namespace EventStore.Core.Services.Transport.Grpc {
	partial class Operations {
		private static readonly Operation ShutdownOperation = new Operation(Plugins.Authorization.Operations.Node.Shutdown);

		private static readonly Operation MergeIndexesOperation =
			new Operation(Plugins.Authorization.Operations.Node.MergeIndexes);

		private static readonly Operation ResignOperation = new Operation(Plugins.Authorization.Operations.Node.Resign);

		private static readonly Operation SetNodePriorityOperation =
			new Operation(Plugins.Authorization.Operations.Node.SetPriority);

		private static readonly Empty EmptyResult = new Empty();

		public override async Task<Empty> Shutdown(Empty request, ServerCallContext context) {
			var user = context.GetHttpContext().User;
			if (!await _authorizationProvider.CheckAccessAsync(user, ShutdownOperation, context.CancellationToken)
				.ConfigureAwait(false)) {
				throw AccessDenied();
			}

			_publisher.Publish(new ClientMessage.RequestShutdown(exitProcess: true, shutdownHttp: true));
			return EmptyResult;
		}

		public override async Task<Empty> MergeIndexes(Empty request, ServerCallContext context) {
			var mergeResultSource = new TaskCompletionSource<string>();

			var user = context.GetHttpContext().User;
			if (!await _authorizationProvider.CheckAccessAsync(user, MergeIndexesOperation, context.CancellationToken)
				.ConfigureAwait(false)) {
				throw AccessDenied();
			}

			var correlationId = Guid.NewGuid();
			_publisher.Publish(new ClientMessage.MergeIndexes(new CallbackEnvelope(OnMessage), correlationId, user));

			await mergeResultSource.Task.ConfigureAwait(false);
			return EmptyResult;

			void OnMessage(Message message) {
				var completed = message as ClientMessage.MergeIndexesResponse;
				if (completed is null) {
					mergeResultSource.TrySetException(
						RpcExceptions.UnknownMessage<ClientMessage.MergeIndexesResponse>(message));
				} else {
					mergeResultSource.SetResult(completed.CorrelationId.ToString());
				}
			}
		}

		public override async Task<Empty> ResignNode(Empty request, ServerCallContext context) {
			var user = context.GetHttpContext().User;
			if (!await _authorizationProvider.CheckAccessAsync(user, ResignOperation, context.CancellationToken)
				.ConfigureAwait(false)) {
				throw AccessDenied();
			}

			_publisher.Publish(new ClientMessage.ResignNode());
			return EmptyResult;
		}

		public override async Task<Empty> SetNodePriority(SetNodePriorityReq request, ServerCallContext context) {
			var user = context.GetHttpContext().User;
			if (!await _authorizationProvider
				.CheckAccessAsync(user, SetNodePriorityOperation, context.CancellationToken)
				.ConfigureAwait(false)) {
				throw AccessDenied();
			}

			_publisher.Publish(new ClientMessage.SetNodePriority(request.Priority));
			return EmptyResult;
		}
	}
}
