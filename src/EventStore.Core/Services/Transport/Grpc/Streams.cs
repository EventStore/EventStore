using System;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using EventStore.Core.Authentication;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Grpc;
using Grpc.Core;

namespace EventStore.Core.Services.Transport.Grpc {
	public partial class Streams : EventStore.Grpc.Streams.Streams.StreamsBase {
		private readonly ClusterVNode _node;

		public Streams(ClusterVNode node) {
			if (node == null) {
				throw new ArgumentNullException(nameof(node));
			}

			_node = node;
		}

		private Task<IPrincipal> GetUser(ServerCallContext context) {
			var principalSource = new TaskCompletionSource<IPrincipal>();

			if (AuthenticationHeaderValue.TryParse(
				    context.RequestHeaders.FirstOrDefault(x => x.Key == Constants.Headers.Authorization)?.Value,
				    out var authenticationHeader)
			    && authenticationHeader.Scheme == Constants.Headers.BasicScheme
			    && TryDecodeCredential(authenticationHeader.Parameter, out var username, out var password)) {
				_node.InternalAuthenticationProvider.Authenticate(
					new GrpcBasicAuthenticationRequest(principalSource, username, password));
			} else {
				principalSource.TrySetResult(default);
			}

			return principalSource.Task;

			bool TryDecodeCredential(string value, out string username, out string password) {
				username = password = default;
				var parts = Encoding.ASCII.GetString(Convert.FromBase64String(value))
					.Split(':'); // TODO: JPB maybe use Convert.TryFromBase64String when in dotnet core 3.0
				if (parts.Length != 2) {
					return false;
				}

				username = parts[0];
				password = parts[1];

				return true;
			}
		}

		private static Exception Timeout() => new RpcException(new Status(StatusCode.Aborted, "Operation timed out"));

		private static Exception ServerNotReady() =>
			new RpcException(new Status(StatusCode.Unavailable, "Server Is Not Ready"));

		private static Exception ServerBusy() =>
			new RpcException(new Status(StatusCode.Unavailable, "Server Is Too Busy"));

		private static Exception NoMasterInfo() =>
			new RpcException(new Status(StatusCode.Unknown, "No master info available in response"));

		private static Exception NotFound(string streamName) =>
			new RpcException(new Status(StatusCode.NotFound, $"Event stream '{streamName}' is not found."), new Metadata {
				{Constants.Exceptions.ExceptionKey, Constants.Exceptions.NotFound},
				{Constants.Exceptions.StreamName, streamName}
			});

		private static Exception NoStream(string streamName) =>
			new RpcException(new Status(StatusCode.NotFound, $"Event stream '{streamName}' was not created."));

		private static Exception UnknownMessage<T>(T message) where T : Message =>
			new RpcException(
				new Status(StatusCode.Unknown,
					$"Envelope callback expected either {typeof(T).Name} or {nameof(ClientMessage.NotHandled)}, received {message.GetType().Name} instead"));

		private static Exception UnknownError<T>(T result) where T : unmanaged =>
			new RpcException(new Status(StatusCode.Unknown, $"Unexpected {typeof(T).Name}: {result}"));

		private static Exception AccessDenied() =>
			new RpcException(new Status(StatusCode.PermissionDenied, "Access Denied"), new Metadata {
				{Constants.Exceptions.ExceptionKey, Constants.Exceptions.AccessDenied}
			});

		private static Exception InvalidTransaction() =>
			new RpcException(new Status(StatusCode.InvalidArgument, "Invalid Transaction"), new Metadata {
				{Constants.Exceptions.ExceptionKey, Constants.Exceptions.InvalidTransaction}
			});

		private static Exception StreamDeleted(string streamName) =>
			new RpcException(new Status(StatusCode.FailedPrecondition, $"Event stream '{streamName}' is deleted."),
				new Metadata {
					{Constants.Exceptions.ExceptionKey, Constants.Exceptions.StreamDeleted},
					{Constants.Exceptions.StreamName, streamName}
				});

		private static Exception WrongExpectedVersion(
			string streamName,
			long expectedVersion,
			long? actualVersion = default) =>
			new RpcException(
				new Status(
					StatusCode.FailedPrecondition,
					$"Append failed due to WrongExpectedVersion. Stream: {streamName}, Expected version: {expectedVersion}"),
				new Metadata {
					{Constants.Exceptions.ExceptionKey, Constants.Exceptions.WrongExpectedVersion},
					{Constants.Exceptions.ExpectedVersion, expectedVersion.ToString()},
					{Constants.Exceptions.ActualVersion, actualVersion?.ToString() ?? string.Empty}
				});

		private static void HandleNotHandled<T>(ClientMessage.NotHandled notHandled, TaskCompletionSource<T> result) {
			switch (notHandled.Reason) {
				case TcpClientMessageDto.NotHandled.NotHandledReason.NotReady:
					result.TrySetException(ServerNotReady());
					return;
				case TcpClientMessageDto.NotHandled.NotHandledReason.TooBusy:
					result.TrySetException(ServerBusy());
					return;
				case TcpClientMessageDto.NotHandled.NotHandledReason.NotMaster:
				case TcpClientMessageDto.NotHandled.NotHandledReason.IsReadOnly:
					switch (notHandled.AdditionalInfo) {
						case TcpClientMessageDto.NotHandled.MasterInfo _:
							return;
						default:
							result.TrySetException(NoMasterInfo());
							return;
					}

				default:
					return;
			}
		}

		class GrpcBasicAuthenticationRequest : AuthenticationRequest {
			private readonly TaskCompletionSource<IPrincipal> _principalSource;

			public GrpcBasicAuthenticationRequest(
				TaskCompletionSource<IPrincipal> principalSource,
				string name,
				string suppliedPassword) : base(name, suppliedPassword) {
				_principalSource = principalSource;
			}

			public override void Authenticated(IPrincipal principal) => _principalSource.TrySetResult(principal);
			public override void Unauthorized() => _principalSource.TrySetException(AccessDenied());
			public override void Error() => _principalSource.TrySetException(UnknownError(1));
			public override void NotReady() => _principalSource.TrySetException(ServerNotReady());
		}
	}
}
