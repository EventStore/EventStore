using System;
using System.Threading.Tasks;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Client.PersistentSubscriptions;
using EventStore.Plugins.Authorization;
using Grpc.Core;
using static EventStore.Core.Messages.ClientMessage.DeletePersistentSubscriptionCompleted;

namespace EventStore.Core.Services.Transport.Grpc {
	internal partial class PersistentSubscriptions {
		private static readonly Operation DeleteOperation = new Operation(Plugins.Authorization.Operations.Subscriptions.Delete);
		public override async Task<DeleteResp> Delete(DeleteReq request, ServerCallContext context) {
			
			var createPersistentSubscriptionSource = new TaskCompletionSource<DeleteResp>();
			var correlationId = Guid.NewGuid();

			var user = context.GetHttpContext().User;

			if (!await _authorizationProvider.CheckAccessAsync(user,
				DeleteOperation, context.CancellationToken).ConfigureAwait(false)) {
				throw AccessDenied();
			}

			_publisher.Publish(new ClientMessage.DeletePersistentSubscription(
				correlationId,
				correlationId,
				new CallbackEnvelope(HandleDeletePersistentSubscriptionCompleted),
				request.Options.StreamIdentifier,
				request.Options.GroupName,
				user));

			return await createPersistentSubscriptionSource.Task.ConfigureAwait(false);

			void HandleDeletePersistentSubscriptionCompleted(Message message) {
				if (message is ClientMessage.NotHandled notHandled && RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
					createPersistentSubscriptionSource.TrySetException(ex);
					return;
				}

				if (!(message is ClientMessage.DeletePersistentSubscriptionCompleted completed)) {
					createPersistentSubscriptionSource.TrySetException(
						RpcExceptions.UnknownMessage<ClientMessage.DeletePersistentSubscriptionCompleted>(message));
					return;
				}

				switch (completed.Result) {
					case DeletePersistentSubscriptionResult.Success:
						createPersistentSubscriptionSource.TrySetResult(new DeleteResp());
						return;
					case DeletePersistentSubscriptionResult.Fail:
						createPersistentSubscriptionSource.TrySetException(RpcExceptions.PersistentSubscriptionFailed(request.Options.StreamIdentifier, request.Options.GroupName, completed.Reason));
						return;
					case DeletePersistentSubscriptionResult.DoesNotExist:
						createPersistentSubscriptionSource.TrySetException(RpcExceptions.PersistentSubscriptionDoesNotExist(request.Options.StreamIdentifier, request.Options.GroupName));
						return;
					case DeletePersistentSubscriptionResult.AccessDenied:
						createPersistentSubscriptionSource.TrySetException(RpcExceptions.AccessDenied());
						return;
					default:
						createPersistentSubscriptionSource.TrySetException(RpcExceptions.UnknownError(completed.Result));
						return;
				}
			}
		}
	}
}
