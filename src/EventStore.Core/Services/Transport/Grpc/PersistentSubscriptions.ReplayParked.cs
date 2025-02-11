// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System;
using System.Threading.Tasks;
using EventStore.Client.PersistentSubscriptions;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Plugins.Authorization;
using Grpc.Core;

namespace EventStore.Core.Services.Transport.Grpc;

internal partial class PersistentSubscriptions {
	private static readonly Operation ReplayParkedOperation = new Operation(Plugins.Authorization.Operations.Subscriptions.ReplayParked);
	public override async Task<ReplayParkedResp> ReplayParked(ReplayParkedReq request, ServerCallContext context) {
		var replayParkedMessagesSource = new TaskCompletionSource<ReplayParkedResp>();
		var correlationId = Guid.NewGuid();

		var user = context.GetHttpContext().User;

		if (!await _authorizationProvider.CheckAccessAsync(user,
			ReplayParkedOperation, context.CancellationToken)) {
			throw RpcExceptions.AccessDenied();
		}

		string streamId = request.Options.StreamOptionCase switch {
			ReplayParkedReq.Types.Options.StreamOptionOneofCase.All => "$all",
			ReplayParkedReq.Types.Options.StreamOptionOneofCase.StreamIdentifier => request.Options.StreamIdentifier,
			_ => throw new InvalidOperationException()
		};

		long? stopAt = request.Options.StopAtOptionCase switch {
			ReplayParkedReq.Types.Options.StopAtOptionOneofCase.StopAt => request.Options.StopAt,
			ReplayParkedReq.Types.Options.StopAtOptionOneofCase.NoLimit => null,
			_ => throw new InvalidOperationException()
		};

		_publisher.Publish(new ClientMessage.ReplayParkedMessages(
			correlationId,
			correlationId,
			new CallbackEnvelope(HandleReplayParkedMessagesCompleted),
			streamId,
			request.Options.GroupName,
			stopAt,
			user));
		return await replayParkedMessagesSource.Task;

		void HandleReplayParkedMessagesCompleted(Message message) {
			if (message is ClientMessage.NotHandled notHandled && RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
				replayParkedMessagesSource.TrySetException(ex);
				return;
			}

			if (message is ClientMessage.ReplayMessagesReceived completed) {
				switch (completed.Result) {
					case ClientMessage.ReplayMessagesReceived.ReplayMessagesReceivedResult.Success:
						replayParkedMessagesSource.TrySetResult(new ReplayParkedResp());
						return;
					case ClientMessage.ReplayMessagesReceived.ReplayMessagesReceivedResult.DoesNotExist:
						replayParkedMessagesSource.TrySetException(
							RpcExceptions.PersistentSubscriptionDoesNotExist(streamId,
								request.Options.GroupName));
						return;
					case ClientMessage.ReplayMessagesReceived.ReplayMessagesReceivedResult.AccessDenied:
						replayParkedMessagesSource.TrySetException(
							RpcExceptions.AccessDenied());
						return;
					case ClientMessage.ReplayMessagesReceived.ReplayMessagesReceivedResult.Fail:
						replayParkedMessagesSource.TrySetException(
							RpcExceptions.PersistentSubscriptionFailed(streamId, request.Options.GroupName,
								completed.Reason));
						return;

					default:
						replayParkedMessagesSource.TrySetException(
							RpcExceptions.UnknownError(completed.Result));
						return;
				}
			}
			replayParkedMessagesSource.TrySetException(
				RpcExceptions.UnknownMessage<ClientMessage.ReplayMessagesReceived>(
					message));
		}
	}
}
