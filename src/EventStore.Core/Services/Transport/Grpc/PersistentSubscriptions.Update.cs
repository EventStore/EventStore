using System;
using System.Threading.Tasks;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Client;
using EventStore.Client.PersistentSubscriptions;
using EventStore.Plugins.Authorization;
using Grpc.Core;

namespace EventStore.Core.Services.Transport.Grpc {
	internal partial class PersistentSubscriptions {
		private static readonly Operation UpdateOperation = new Operation(Plugins.Authorization.Operations.Subscriptions.Update);
		public override async Task<UpdateResp> Update(UpdateReq request, ServerCallContext context) {
			var updatePersistentSubscriptionSource = new TaskCompletionSource<UpdateResp>();
			var settings = request.Options.Settings;
			var correlationId = Guid.NewGuid();

			var user = context.GetHttpContext().User;
			if (!await _authorizationProvider.CheckAccessAsync(user,
				UpdateOperation, context.CancellationToken).ConfigureAwait(false)) {
				throw AccessDenied();
			}
			_publisher.Publish(new ClientMessage.UpdatePersistentSubscription(
				correlationId,
				correlationId,
				new CallbackEnvelope(HandleUpdatePersistentSubscriptionCompleted),
				request.Options.StreamIdentifier,
				request.Options.GroupName,
				settings.ResolveLinks,
				new StreamRevision(settings.Revision).ToInt64(),
				settings.MessageTimeoutCase switch {
					UpdateReq.Types.Settings.MessageTimeoutOneofCase.MessageTimeoutMs => settings.MessageTimeoutMs,
					UpdateReq.Types.Settings.MessageTimeoutOneofCase.MessageTimeoutTicks => (int)TimeSpan
						.FromTicks(settings.MessageTimeoutTicks).TotalMilliseconds,
					_ => 0
				},
				settings.ExtraStatistics,
				settings.MaxRetryCount,
				settings.HistoryBufferSize,
				settings.LiveBufferSize,
				settings.ReadBatchSize,
				settings.CheckpointAfterCase switch {
					UpdateReq.Types.Settings.CheckpointAfterOneofCase.CheckpointAfterMs => settings.CheckpointAfterMs,
					UpdateReq.Types.Settings.CheckpointAfterOneofCase.CheckpointAfterTicks => (int)TimeSpan
						.FromTicks(settings.CheckpointAfterTicks).TotalMilliseconds,
					_ => 0
				},
				settings.MinCheckpointCount,
				settings.MaxCheckpointCount,
				settings.MaxSubscriberCount,
				settings.NamedConsumerStrategy.ToString(),
				user));

			return await updatePersistentSubscriptionSource.Task.ConfigureAwait(false);

			void HandleUpdatePersistentSubscriptionCompleted(Message message) {
				if (message is ClientMessage.NotHandled notHandled && RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
					updatePersistentSubscriptionSource.TrySetException(ex);
					return;
				}

				if (!(message is ClientMessage.UpdatePersistentSubscriptionCompleted completed)) {
					updatePersistentSubscriptionSource.TrySetException(
						RpcExceptions.UnknownMessage<ClientMessage.UpdatePersistentSubscriptionCompleted>(message));
					return;
				}

				switch (completed.Result) {
					case ClientMessage.UpdatePersistentSubscriptionCompleted.UpdatePersistentSubscriptionResult.Success:
						updatePersistentSubscriptionSource.TrySetResult(new UpdateResp());
						return;
					case ClientMessage.UpdatePersistentSubscriptionCompleted.UpdatePersistentSubscriptionResult.Fail:
						updatePersistentSubscriptionSource.TrySetException(RpcExceptions.PersistentSubscriptionFailed(request.Options.StreamIdentifier, request.Options.GroupName, completed.Reason));
						return;
					case ClientMessage.UpdatePersistentSubscriptionCompleted.UpdatePersistentSubscriptionResult
						.AccessDenied:
						updatePersistentSubscriptionSource.TrySetException(RpcExceptions.AccessDenied());
						return;
					default:
						updatePersistentSubscriptionSource.TrySetException(RpcExceptions.UnknownError(completed.Result));
						return;
				}
			}
		}
	}
}
