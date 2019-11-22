using System;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Grpc;
using Grpc.Core;

namespace EventStore.Core.Services.Transport.Grpc
{
	internal static class RpcExceptions {
		public static Exception Timeout() => new RpcException(new Status(StatusCode.Aborted, "Operation timed out"));

		public static Exception ServerNotReady() =>
			new RpcException(new Status(StatusCode.Unavailable, "Server Is Not Ready"));

		private static Exception ServerBusy() =>
			new RpcException(new Status(StatusCode.Unavailable, "Server Is Too Busy"));

		private static Exception NoMasterInfo() =>
			new RpcException(new Status(StatusCode.Unknown, "No master info available in response"));

		public static Exception StreamNotFound(string streamName) =>
			new RpcException(new Status(StatusCode.NotFound, $"Event stream '{streamName}' is not found."), new Metadata {
				{Constants.Exceptions.ExceptionKey, Constants.Exceptions.StreamNotFound},
				{Constants.Exceptions.StreamName, streamName}
			});

		public static Exception NoStream(string streamName) =>
			new RpcException(new Status(StatusCode.NotFound, $"Event stream '{streamName}' was not created."));

		public static Exception UnknownMessage<T>(Message message) where T : Message =>
			new RpcException(
				new Status(StatusCode.Unknown,
					$"Envelope callback expected either {typeof(T).Name} or {nameof(ClientMessage.NotHandled)}, received {message.GetType().Name} instead"));

		public static Exception UnknownError<T>(T result) where T : unmanaged =>
			new RpcException(new Status(StatusCode.Unknown, $"Unexpected {typeof(T).Name}: {result}"));

		public static Exception UnknownError(string message) =>
			new RpcException(new Status(StatusCode.Unknown, message));

		public static Exception AccessDenied() =>
			new RpcException(new Status(StatusCode.PermissionDenied, "Access Denied"), new Metadata {
				{Constants.Exceptions.ExceptionKey, Constants.Exceptions.AccessDenied}
			});

		public static Exception InvalidTransaction() =>
			new RpcException(new Status(StatusCode.InvalidArgument, "Invalid Transaction"), new Metadata {
				{Constants.Exceptions.ExceptionKey, Constants.Exceptions.InvalidTransaction}
			});

		public static Exception StreamDeleted(string streamName) =>
			new RpcException(new Status(StatusCode.FailedPrecondition, $"Event stream '{streamName}' is deleted."),
				new Metadata {
					{Constants.Exceptions.ExceptionKey, Constants.Exceptions.StreamDeleted},
					{Constants.Exceptions.StreamName, streamName}
				});

		public static Exception WrongExpectedVersion(
			string streamName,
			long expectedVersion,
			long? actualVersion = default) =>
			new RpcException(
				new Status(
					StatusCode.FailedPrecondition,
					$"Append failed due to WrongExpectedVersion. Stream: {streamName}, Expected version: {expectedVersion}, Actual version: {actualVersion}"),
				new Metadata {
					{Constants.Exceptions.ExceptionKey, Constants.Exceptions.WrongExpectedVersion},
					{Constants.Exceptions.StreamName, streamName},
					{Constants.Exceptions.ExpectedVersion, expectedVersion.ToString()},
					{Constants.Exceptions.ActualVersion, actualVersion?.ToString() ?? string.Empty}
				});

		public static bool TryHandleNotHandled(ClientMessage.NotHandled notHandled, out Exception exception) {
			exception = null;
			switch (notHandled.Reason) {
				case TcpClientMessageDto.NotHandled.NotHandledReason.NotReady:
					exception = ServerNotReady();
					return true;
				case TcpClientMessageDto.NotHandled.NotHandledReason.TooBusy:
					exception = ServerBusy();
					return true;
				case TcpClientMessageDto.NotHandled.NotHandledReason.NotMaster:
				case TcpClientMessageDto.NotHandled.NotHandledReason.IsReadOnly:
					switch (notHandled.AdditionalInfo) {
						case TcpClientMessageDto.NotHandled.MasterInfo _:
							return false;
						default:
							exception = NoMasterInfo();
							return true;
					}

				default:
					return false;
			}
		}

		public static Exception PersistentSubscriptionFailed(string streamName, string groupName, string reason)
			=> new RpcException(
				new Status(
					StatusCode.Internal,
					$"Subscription group {groupName} on stream {streamName} failed: '{reason}'"), new Metadata {
					{Constants.Exceptions.ExceptionKey, Constants.Exceptions.PersistentSubscriptionFailed},
					{Constants.Exceptions.StreamName, streamName},
					{Constants.Exceptions.GroupName, groupName},
					{Constants.Exceptions.Reason, reason}
				});

		public static Exception PersistentSubscriptionDoesNotExist(string streamName, string groupName)
			=> new RpcException(
				new Status(
					StatusCode.NotFound,
					$"Subscription group {groupName} on stream {streamName} does not exist."), new Metadata {
					{Constants.Exceptions.ExceptionKey, Constants.Exceptions.PersistentSubscriptionDoesNotExist},
					{Constants.Exceptions.StreamName, streamName},
					{Constants.Exceptions.GroupName, groupName}
				});

		public static Exception PersistentSubscriptionExists(string streamName, string groupName)
			=> new RpcException(
				new Status(
					StatusCode.AlreadyExists,
					$"Subscription group {groupName} on stream {streamName} exists."), new Metadata {
					{Constants.Exceptions.ExceptionKey, Constants.Exceptions.PersistentSubscriptionExists},
					{Constants.Exceptions.StreamName, streamName},
					{Constants.Exceptions.GroupName, groupName}
				});

		public static Exception PersistentSubscriptionMaximumSubscribersReached(string streamName, string groupName)
			=> new RpcException(
				new Status(
					StatusCode.FailedPrecondition,
					$"Maximum subscriptions reached for subscription group {groupName} on stream {streamName}."), new Metadata {
					{Constants.Exceptions.ExceptionKey, Constants.Exceptions.MaximumSubscribersReached},
					{Constants.Exceptions.StreamName, streamName},
					{Constants.Exceptions.GroupName, groupName}
				});

		public static Exception PersistentSubscriptionDropped(string streamName, string groupName)
			=> new RpcException(
				new Status(
					StatusCode.Cancelled,
					$"Subscription group {groupName} on stream {streamName} was dropped."), new Metadata {
					{Constants.Exceptions.ExceptionKey, Constants.Exceptions.PersistentSubscriptionDropped},
					{Constants.Exceptions.StreamName, streamName},
					{Constants.Exceptions.GroupName, groupName}
				});

		public static Exception LoginNotFound(string loginName) =>
			new RpcException(new Status(StatusCode.NotFound, $"User '{loginName}' is not found."), new Metadata {
				{Constants.Exceptions.ExceptionKey, Constants.Exceptions.UserNotFound},
				{Constants.Exceptions.LoginName, loginName}
			});

		public static Exception LoginConflict(string loginName) =>
			new RpcException(new Status(StatusCode.FailedPrecondition, "Conflict."), new Metadata {
				{Constants.Exceptions.ExceptionKey, Constants.Exceptions.UserConflict},
				{Constants.Exceptions.LoginName, loginName}
			});

		public static Exception LoginTryAgain(string loginName) =>
			new RpcException(new Status(StatusCode.DeadlineExceeded, "Try again."), new Metadata {
				{Constants.Exceptions.ExceptionKey, Constants.Exceptions.UserConflict},
				{Constants.Exceptions.LoginName, loginName}
			});

	}
}
