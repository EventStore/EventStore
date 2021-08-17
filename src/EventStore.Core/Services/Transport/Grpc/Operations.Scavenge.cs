using System;
using System.Threading.Tasks;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Client.Operations;
using EventStore.Plugins.Authorization;
using Grpc.Core;

namespace EventStore.Core.Services.Transport.Grpc {
	partial class Operations {
		private static readonly Operation StartOperation = new Operation(Plugins.Authorization.Operations.Node.Scavenge.Start);
		private static readonly Operation StopOperation = new Operation(Plugins.Authorization.Operations.Node.Scavenge.Stop);
		public override async Task<ScavengeResp> StartScavenge(StartScavengeReq request, ServerCallContext context) {
			var scavengeResultSource = new TaskCompletionSource<(string, ScavengeResp.Types.ScavengeResult)>();

			var user = context.GetHttpContext().User;
			if (!await _authorizationProvider.CheckAccessAsync(user, StartOperation, context.CancellationToken).ConfigureAwait(false)) {
				throw RpcExceptions.AccessDenied();
			}
			_publisher.Publish(new ClientMessage.ScavengeDatabase(new CallbackEnvelope(OnMessage), Guid.NewGuid(), user,
				request.Options.StartFromChunk, request.Options.ThreadCount));

			var (scavengeId, scavengeResult) = await scavengeResultSource.Task.ConfigureAwait(false);

			return new ScavengeResp {
				ScavengeId = scavengeId,
				ScavengeResult = scavengeResult
			};

			void OnMessage(Message message) => HandleScavengeDatabaseResponse(message, scavengeResultSource);
		}

		public override async Task<ScavengeResp> StopScavenge(StopScavengeReq request, ServerCallContext context) {
			var scavengeResultSource = new TaskCompletionSource<(string, ScavengeResp.Types.ScavengeResult)>();

			var user = context.GetHttpContext().User;
			if (!await _authorizationProvider.CheckAccessAsync(user, StopOperation, context.CancellationToken).ConfigureAwait(false)) {
				throw RpcExceptions.AccessDenied();
			}
			_publisher.Publish(new ClientMessage.StopDatabaseScavenge(new CallbackEnvelope(OnMessage), Guid.NewGuid(), user,
				request.Options.ScavengeId));

			var (scavengeId, scavengeResult) = await scavengeResultSource.Task.ConfigureAwait(false);

			return new ScavengeResp {
				ScavengeId = scavengeId,
				ScavengeResult = scavengeResult
			};

			void OnMessage(Message message) => HandleScavengeDatabaseResponse(message, scavengeResultSource);
		}

		private static void HandleScavengeDatabaseResponse(
			Message message,
			TaskCompletionSource<(string, ScavengeResp.Types.ScavengeResult)> scavengeResultSource) {
			if (!(message is ClientMessage.ScavengeDatabaseResponse response)) {
				scavengeResultSource.TrySetException(
					RpcExceptions.UnknownMessage<ClientMessage.ScavengeDatabaseResponse>(message));
				return;
			}

			switch (response.Result) {
				case ClientMessage.ScavengeDatabaseResponse.ScavengeResult.Unauthorized:
					scavengeResultSource.TrySetException(RpcExceptions.AccessDenied());
					return;
				case ClientMessage.ScavengeDatabaseResponse.ScavengeResult.InvalidScavengeId:
					scavengeResultSource.TrySetException(RpcExceptions.ScavengeNotFound(response.ScavengeId));
					return;
				case ClientMessage.ScavengeDatabaseResponse.ScavengeResult.Started:
					scavengeResultSource.TrySetResult((response.ScavengeId,
						ScavengeResp.Types.ScavengeResult.Started));
					return;
				case ClientMessage.ScavengeDatabaseResponse.ScavengeResult.Stopped:
					scavengeResultSource.TrySetResult((response.ScavengeId,
						ScavengeResp.Types.ScavengeResult.Stopped));
					return;
				case ClientMessage.ScavengeDatabaseResponse.ScavengeResult.InProgress:
					scavengeResultSource.TrySetResult((response.ScavengeId,
						ScavengeResp.Types.ScavengeResult.InProgress));
					return;
			}
		}
	}
}
