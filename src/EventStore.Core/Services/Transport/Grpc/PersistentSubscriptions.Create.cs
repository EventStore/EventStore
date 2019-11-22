using System;
using System.Security.Principal;
using System.Threading.Tasks;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Grpc;
using EventStore.Grpc.PersistentSubscriptions;
using Grpc.Core;
using static EventStore.Core.Messages.ClientMessage.CreatePersistentSubscriptionCompleted;

namespace EventStore.Core.Services.Transport.Grpc {
	partial class PersistentSubscriptions {
		public override async Task<CreateResp> Create(CreateReq request, ServerCallContext context) {
			var createPersistentSubscriptionSource = new TaskCompletionSource<CreateResp>();
			var settings = request.Options.Settings;
			var correlationId = Guid.NewGuid();

			var user = await GetUser(_authenticationProvider, context.RequestHeaders);

			_queue.Publish(new ClientMessage.CreatePersistentSubscription(
				correlationId,
				correlationId,
				new CallbackEnvelope(HandleCreatePersistentSubscriptionCompleted),
				request.Options.StreamName,
				request.Options.GroupName,
				settings.ResolveLinks,
				new StreamRevision(settings.Revision).ToInt64(),
				(int)TimeSpan.FromTicks(settings.MessageTimeout).TotalMilliseconds,
				settings.ExtraStatistics,
				settings.MaxRetryCount,
				settings.HistoryBufferSize,
				settings.LiveBufferSize,
				settings.ReadBatchSize,
				(int)TimeSpan.FromTicks(settings.CheckpointAfter).TotalMilliseconds,
				settings.MinCheckpointCount,
				settings.MaxCheckpointCount,
				settings.MaxSubscriberCount,
				settings.NamedConsumerStrategy.ToString(),
				user,
				default,
				default));

			return await createPersistentSubscriptionSource.Task;

			void HandleCreatePersistentSubscriptionCompleted(Message message) {
				if (message is ClientMessage.NotHandled notHandled && RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
					createPersistentSubscriptionSource.TrySetException(ex);
					return;
				}

				if (!(message is ClientMessage.CreatePersistentSubscriptionCompleted completed)) {
					createPersistentSubscriptionSource.TrySetException(
						RpcExceptions.UnknownMessage<ClientMessage.CreatePersistentSubscriptionCompleted>(message));
					return;
				}

				switch (completed.Result) {
					case CreatePersistentSubscriptionResult.Success:
						createPersistentSubscriptionSource.TrySetResult(new CreateResp());
						return;
					case CreatePersistentSubscriptionResult.Fail:
						createPersistentSubscriptionSource.TrySetException(RpcExceptions.PersistentSubscriptionFailed(request.Options.StreamName, request.Options.GroupName, completed.Reason));
						return;
					case CreatePersistentSubscriptionResult.AlreadyExists:
						createPersistentSubscriptionSource.TrySetException(RpcExceptions.PersistentSubscriptionExists(request.Options.StreamName, request.Options.GroupName));
						return;
					case CreatePersistentSubscriptionResult.AccessDenied:
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
