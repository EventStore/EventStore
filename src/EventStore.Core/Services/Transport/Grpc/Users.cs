using System;
using System.Threading.Tasks;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using static EventStore.Core.Messages.UserManagementMessage;

namespace EventStore.Core.Services.Transport.Grpc {
	public partial class Users : EventStore.Client.Users.Users.UsersBase {
		private readonly IQueuedHandler _queue;
		
		public Users(IQueuedHandler queue) {
			if (queue == null) throw new ArgumentNullException(nameof(queue));
			_queue = queue;
		}

		private static bool HandleErrors<T>(string loginName, Message message, TaskCompletionSource<T> source) {
			if (!(message is ResponseMessage response)) {
				source.TrySetException(
					RpcExceptions.UnknownMessage<ResponseMessage>(message));
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
}
