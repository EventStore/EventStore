using EventStore.Client;
using EventStore.Core.Services.Transport.Grpc;
using Google.Protobuf.WellKnownTypes;
using Empty = Google.Protobuf.WellKnownTypes.Empty;

// ReSharper disable once CheckNamespace
namespace EventStore.Client {
	partial class Status {
		public static Status WrongExpectedVersion(StreamRevision currentStreamRevision,
			long expectedVersion) => new() {
			Message = nameof(WrongExpectedVersion),
			Details = Any.Pack(EventStore.Client.WrongExpectedVersion.Create(currentStreamRevision, expectedVersion)),
			Code = EventStore.Client.Code.AlreadyExists
		};

		public static Status StreamDeleted(StreamIdentifier streamIdentifier) => new() {
			Details = Any.Pack(new StreamDeleted {
				StreamIdentifier = streamIdentifier
			}),
			Message = nameof(StreamDeleted),
			Code = EventStore.Client.Code.NotFound
		};

		public static Status AccessDenied { get; } = new() {
			Details = Any.Pack(new AccessDenied()),
			Message = nameof(AccessDenied),
			Code = EventStore.Client.Code.PermissionDenied
		};

		public static Status Timeout { get; } = new() {
			Details = Any.Pack(new Timeout()),
			Message = nameof(Timeout),
			Code = EventStore.Client.Code.DeadlineExceeded
		};

		public static Status InvalidTransaction { get; } = new() {
			Details = Any.Pack(new InvalidTransaction()),
			Message = nameof(InvalidTransaction),
			Code = EventStore.Client.Code.FailedPrecondition
		};

		public static Status Unknown { get; } = new() {
			Details = Any.Pack(new Unknown()),
			Message = nameof(Unknown),
			Code = EventStore.Client.Code.Unknown
		};

		public static Status MaximumAppendSizeExceeded(uint maxAppendSize) =>
			new() {
				Details = Any.Pack(new MaximumAppendSizeExceeded {
					MaxAppendSize = maxAppendSize
				}),
				Message = nameof(MaximumAppendSizeExceeded),
				Code = EventStore.Client.Code.InvalidArgument
			};

		public static Status BadRequest(string message) => new() {
			Details = Any.Pack(new BadRequest {Message = message}),
			Message = nameof(BadRequest),
			Code = EventStore.Client.Code.InvalidArgument
		};

		public static Status InternalError(string message) => new() {
			Details = Any.Pack(new Empty()),
			Message = message,
			Code = EventStore.Client.Code.Internal
		};
	}
}
