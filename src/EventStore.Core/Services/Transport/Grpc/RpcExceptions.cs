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

		public static Exception NotFound(string streamName) =>
			new RpcException(new Status(StatusCode.NotFound, $"Event stream '{streamName}' is not found."), new Metadata {
				{Constants.Exceptions.ExceptionKey, Constants.Exceptions.NotFound},
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
	}
}
