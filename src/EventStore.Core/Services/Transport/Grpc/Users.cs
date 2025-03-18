// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Plugins.Authorization;
using static EventStore.Core.Messages.UserManagementMessage;

namespace EventStore.Core.Services.Transport.Grpc;

internal partial class Users(IPublisher publisher, IAuthorizationProvider authorizationProvider) : EventStore.Client.Users.Users.UsersBase {
	private readonly IPublisher _publisher = Ensure.NotNull(publisher);
	private readonly IAuthorizationProvider _authorizationProvider = Ensure.NotNull(authorizationProvider);

	private static bool HandleErrors<T>(string loginName, Message message, TaskCompletionSource<T> source) {
		if (message is not ResponseMessage response) {
			source.TrySetException(RpcExceptions.UnknownMessage<ResponseMessage>(message));
			return true;
		}

		if (response.Success) return false;
		source.TrySetException(response.Error switch {
			Error.Unauthorized => RpcExceptions.AccessDenied(),
			Error.NotFound => RpcExceptions.LoginNotFound(loginName),
			Error.Conflict => RpcExceptions.LoginConflict(loginName),
			Error.TryAgain => RpcExceptions.LoginTryAgain(loginName),
			_ => RpcExceptions.UnknownError(response.Error)
		});
		return true;
	}
}
