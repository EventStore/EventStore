using System;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Grpc;
using EventStore.Client;
using Grpc.Core;

namespace EventStore.Cluster {
	partial class Gossip {
		partial class GossipBase : ServiceBase {
			
		}
	}

	partial class Elections {
		partial class ElectionsBase : ServiceBase {

		}
	}
}

namespace EventStore.Client.PersistentSubscriptions {
	partial class PersistentSubscriptions {
		partial class PersistentSubscriptionsBase : ServiceBase {
		}
	}
}

namespace EventStore.Client.Streams {
	partial class Streams {
		partial class StreamsBase : ServiceBase {
		}
	}
}

namespace EventStore.Client.Users {
	partial class Users {
		partial class UsersBase : ServiceBase {
		}
	}
}

namespace EventStore.Client.Operations {
	partial class Operations {
		partial class OperationsBase : ServiceBase {
		}
	}
}

namespace EventStore.Client.Gossip {
	partial class Gossip {
		partial class GossipBase : ServiceBase {
		}
	}
}

namespace EventStore.Client.Monitoring {
	partial class Monitoring {
		partial class MonitoringBase : ServiceBase {
		}
	}
}

namespace EventStore.Core.Services.Transport.Grpc {
	public class ServiceBase {
		
		public static bool GetRequiresLeader(Metadata requestHeaders) {
			var requiresLeaderHeaderValue =
				requestHeaders.FirstOrDefault(x => x.Key == Constants.Headers.RequiresLeader)?.Value;
			if (string.IsNullOrEmpty(requiresLeaderHeaderValue)) return false;
			bool.TryParse(requiresLeaderHeaderValue, out var requiresLeader);
			return requiresLeader;
		}

		public static Exception Timeout() => new RpcException(new Status(StatusCode.Aborted, "Operation timed out"));

		public static Exception ServerNotReady() =>
			new RpcException(new Status(StatusCode.Unavailable, "Server Is Not Ready"));

		private static Exception ServerBusy() =>
			new RpcException(new Status(StatusCode.Unavailable, "Server Is Too Busy"));

		private static Exception NoLeaderInfo() =>
			new RpcException(new Status(StatusCode.Unknown, "No leader info available in response"));

		public static Exception NotFound(string streamName) =>
			new RpcException(new Status(StatusCode.NotFound, $"Event stream '{streamName}' is not found."), new Metadata {
				{Constants.Exceptions.ExceptionKey, Constants.Exceptions.StreamNotFound},
				{Constants.Exceptions.StreamName, streamName}
			});

		private static Exception NoStream(string streamName) =>
			new RpcException(new Status(StatusCode.NotFound, $"Event stream '{streamName}' was not created."));

		public static Exception UnknownMessage<T>(T message) where T : Message =>
			new RpcException(
				new Status(StatusCode.Unknown,
					$"Envelope callback expected either {typeof(T).Name} or {nameof(ClientMessage.NotHandled)}, received {message.GetType().Name} instead"));

		public static Exception UnknownError<T>(T result) where T : unmanaged =>
			new RpcException(new Status(StatusCode.Unknown, $"Unexpected {typeof(T).Name}: {result}"));

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
					$"Append failed due to WrongExpectedVersion. Stream: {streamName}, Expected version: {expectedVersion}"),
				new Metadata {
					{Constants.Exceptions.ExceptionKey, Constants.Exceptions.WrongExpectedVersion},
					{Constants.Exceptions.ExpectedVersion, expectedVersion.ToString()},
					{Constants.Exceptions.ActualVersion, actualVersion?.ToString() ?? string.Empty}
				});

		public static void HandleNotHandled<T>(ClientMessage.NotHandled notHandled, TaskCompletionSource<T> result) {
			switch (notHandled.Reason) {
				case TcpClientMessageDto.NotHandled.NotHandledReason.NotReady:
					result.TrySetException(ServerNotReady());
					return;
				case TcpClientMessageDto.NotHandled.NotHandledReason.TooBusy:
					result.TrySetException(ServerBusy());
					return;
				case TcpClientMessageDto.NotHandled.NotHandledReason.NotLeader:
				case TcpClientMessageDto.NotHandled.NotHandledReason.IsReadOnly:
					switch (notHandled.AdditionalInfo) {
						case TcpClientMessageDto.NotHandled.LeaderInfo _:
							return;
						default:
							result.TrySetException(NoLeaderInfo());
							return;
					}

				default:
					return;
			}
		}
	}
}
