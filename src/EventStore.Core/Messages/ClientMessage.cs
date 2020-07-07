using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Settings;
using EventStore.Core.Util;
using static EventStore.Core.Messages.TcpClientMessageDto.FilteredReadAllEventsCompleted;
using FilteredReadAllResult = EventStore.Core.Data.FilteredReadAllResult;
using ReadStreamResult = EventStore.Core.Data.ReadStreamResult;

namespace EventStore.Core.Messages {
	public enum OperationResult {
		Success = 0,
		PrepareTimeout = 1,
		CommitTimeout = 2,
		ForwardTimeout = 3,
		WrongExpectedVersion = 4,
		StreamDeleted = 5,
		InvalidTransaction = 6,
		AccessDenied = 7
	}

	public static class ClientMessage {
		public class RequestShutdown : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly bool ExitProcess;

			public readonly bool ShutdownHttp;

			public RequestShutdown(bool exitProcess, bool shutdownHttp) {
				ExitProcess = exitProcess;
				ShutdownHttp = shutdownHttp;
			}
		}

		public class ReloadConfig : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
			public override int MsgTypeId {
				get { return TypeId; }
			}
		}

		public abstract class WriteRequestMessage : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid InternalCorrId;
			public readonly Guid CorrelationId;
			public readonly IEnvelope Envelope;
			public readonly bool RequireLeader;

			public readonly ClaimsPrincipal User;
			public string Login => Tokens?.GetValueOrDefault("uid");
			public string Password => Tokens?.GetValueOrDefault("pwd");
			public readonly IReadOnlyDictionary<string, string> Tokens;

			protected WriteRequestMessage(Guid internalCorrId,
				Guid correlationId, IEnvelope envelope, bool requireLeader,
				ClaimsPrincipal user, IReadOnlyDictionary<string, string> tokens) {
				Ensure.NotEmptyGuid(internalCorrId, "internalCorrId");
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				Ensure.NotNull(envelope, "envelope");

				InternalCorrId = internalCorrId;
				CorrelationId = correlationId;
				Envelope = envelope;
				RequireLeader = requireLeader;

				User = user;
				Tokens = tokens;
			}
		}

		public abstract class ReadRequestMessage : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid InternalCorrId;
			public readonly Guid CorrelationId;
			public readonly IEnvelope Envelope;

			public readonly ClaimsPrincipal User;

			public readonly DateTime Expires;

			protected ReadRequestMessage(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				ClaimsPrincipal user, DateTime? expires) {
				Ensure.NotEmptyGuid(internalCorrId, "internalCorrId");
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				Ensure.NotNull(envelope, "envelope");

				InternalCorrId = internalCorrId;
				CorrelationId = correlationId;
				Envelope = envelope;

				User = user;
				Expires = expires ?? DateTime.UtcNow.AddMilliseconds(ESConsts.ReadRequestTimeout);
			}
		}

		public abstract class ReadResponseMessage : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
		}

		public class TcpForwardMessage : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Message Message;

			public TcpForwardMessage(Message message) {
				Ensure.NotNull(message, "message");

				Message = message;
			}
		}

		public class NotHandled : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly TcpClientMessageDto.NotHandled.NotHandledReason Reason;
			public readonly object AdditionalInfo;

			public NotHandled(Guid correlationId,
				TcpClientMessageDto.NotHandled.NotHandledReason reason,
				object additionalInfo) {
				CorrelationId = correlationId;
				Reason = reason;
				AdditionalInfo = additionalInfo;
			}
		}

		public class WriteEvents : WriteRequestMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly string EventStreamId;
			public readonly long ExpectedVersion;
			public readonly Event[] Events;
			public readonly CancellationToken CancellationToken;

			public WriteEvents(Guid internalCorrId, Guid correlationId, IEnvelope envelope, bool requireLeader,
				string eventStreamId, long expectedVersion, Event[] events, ClaimsPrincipal user,
				IReadOnlyDictionary<string, string> tokens = null, CancellationToken cancellationToken = default)
				: base(internalCorrId, correlationId, envelope, requireLeader, user, tokens) {
				Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
				if (expectedVersion < Data.ExpectedVersion.StreamExists ||
				    expectedVersion == Data.ExpectedVersion.Invalid)
					throw new ArgumentOutOfRangeException(nameof(expectedVersion));
				Ensure.NotNull(events, "events");

				EventStreamId = eventStreamId;
				ExpectedVersion = expectedVersion;
				Events = events;
				CancellationToken = cancellationToken;
			}

			public WriteEvents(Guid internalCorrId, Guid correlationId, IEnvelope envelope, bool requireLeader,
				string eventStreamId, long expectedVersion, Event @event, ClaimsPrincipal user,
				IReadOnlyDictionary<string, string> tokens = null)
				: this(internalCorrId, correlationId, envelope, requireLeader, eventStreamId, expectedVersion,
					@event == null ? null : new[] {@event}, user, tokens) {
			}

			public override string ToString() {
				return String.Format(
					"WRITE: InternalCorrId: {0}, CorrelationId: {1}, EventStreamId: {2}, ExpectedVersion: {3}, Events: {4}",
					InternalCorrId, CorrelationId, EventStreamId, ExpectedVersion, Events.Length);
			}
		}

		public class WriteEventsCompleted : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly OperationResult Result;
			public readonly string Message;
			public readonly long FirstEventNumber;
			public readonly long LastEventNumber;
			public readonly long PreparePosition;
			public readonly long CommitPosition;
			public readonly long CurrentVersion;

			public WriteEventsCompleted(Guid correlationId, long firstEventNumber, long lastEventNumber,
				long preparePosition, long commitPosition) {
				if (firstEventNumber < -1)
					throw new ArgumentOutOfRangeException(nameof(firstEventNumber),
						$"FirstEventNumber: {firstEventNumber}");
				if (lastEventNumber - firstEventNumber + 1 < 0)
					throw new ArgumentOutOfRangeException(nameof(lastEventNumber),
						$"LastEventNumber {lastEventNumber}, FirstEventNumber {firstEventNumber}.");

				CorrelationId = correlationId;
				Result = OperationResult.Success;
				Message = null;
				FirstEventNumber = firstEventNumber;
				LastEventNumber = lastEventNumber;
				PreparePosition = preparePosition;
				CommitPosition = commitPosition;
			}

			public WriteEventsCompleted(Guid correlationId, OperationResult result, string message,
				long currentVersion = -1) {
				if (result == OperationResult.Success)
					throw new ArgumentException("Invalid constructor used for successful write.", nameof(result));

				CorrelationId = correlationId;
				Result = result;
				Message = message;
				FirstEventNumber = EventNumber.Invalid;
				LastEventNumber = EventNumber.Invalid;
				PreparePosition = EventNumber.Invalid;
				CurrentVersion = currentVersion;
			}

			private WriteEventsCompleted(Guid correlationId, OperationResult result, string message,
				long firstEventNumber, long lastEventNumber, long preparePosition, long commitPosition,
				long currentVersion) {
				CorrelationId = correlationId;
				Result = result;
				Message = message;
				FirstEventNumber = firstEventNumber;
				LastEventNumber = lastEventNumber;
				PreparePosition = preparePosition;
				CommitPosition = commitPosition;
				CurrentVersion = currentVersion;
			}

			public WriteEventsCompleted WithCorrelationId(Guid newCorrId) {
				return new WriteEventsCompleted(newCorrId, Result, Message, FirstEventNumber, LastEventNumber,
					PreparePosition, CommitPosition, CurrentVersion);
			}

			public override string ToString() {
				return
					$"WRITE COMPLETED: CorrelationId: {CorrelationId}, Result: {Result}, Message: {Message}, FirstEventNumber: {FirstEventNumber}, LastEventNumber: {LastEventNumber}, CurrentVersion: {CurrentVersion}";
			}
		}

		public class TransactionStart : WriteRequestMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly string EventStreamId;
			public readonly long ExpectedVersion;

			public TransactionStart(Guid internalCorrId, Guid correlationId, IEnvelope envelope, bool requireLeader,
				string eventStreamId, long expectedVersion, ClaimsPrincipal user,
				IReadOnlyDictionary<string, string> tokens = null)
				: base(internalCorrId, correlationId, envelope, requireLeader, user, tokens) {
				Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
				if (expectedVersion < Data.ExpectedVersion.Any)
					throw new ArgumentOutOfRangeException(nameof(expectedVersion));

				EventStreamId = eventStreamId;
				ExpectedVersion = expectedVersion;
			}
		}

		public class TransactionStartCompleted : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly long TransactionId;
			public readonly OperationResult Result;
			public readonly string Message;

			public TransactionStartCompleted(Guid correlationId, long transactionId, OperationResult result,
				string message) {
				CorrelationId = correlationId;
				TransactionId = transactionId;
				Result = result;
				Message = message;
			}

			public TransactionStartCompleted WithCorrelationId(Guid newCorrId) {
				return new TransactionStartCompleted(newCorrId, TransactionId, Result, Message);
			}
		}

		public class TransactionWrite : WriteRequestMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly long TransactionId;
			public readonly Event[] Events;

			public TransactionWrite(Guid internalCorrId, Guid correlationId, IEnvelope envelope, bool requireLeader,
				long transactionId, Event[] events, ClaimsPrincipal user, IReadOnlyDictionary<string, string> tokens = null)
				: base(internalCorrId, correlationId, envelope, requireLeader, user, tokens) {
				Ensure.Nonnegative(transactionId, "transactionId");
				Ensure.NotNull(events, "events");

				TransactionId = transactionId;
				Events = events;
			}
		}

		public class TransactionWriteCompleted : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly long TransactionId;
			public readonly OperationResult Result;
			public readonly string Message;

			public TransactionWriteCompleted(Guid correlationId, long transactionId, OperationResult result,
				string message) {
				CorrelationId = correlationId;
				TransactionId = transactionId;
				Result = result;
				Message = message;
			}

			public TransactionWriteCompleted WithCorrelationId(Guid newCorrId) {
				return new TransactionWriteCompleted(newCorrId, TransactionId, Result, Message);
			}
		}

		public class TransactionCommit : WriteRequestMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly long TransactionId;

			public TransactionCommit(Guid internalCorrId, Guid correlationId, IEnvelope envelope, bool requireLeader,
				long transactionId, ClaimsPrincipal user, IReadOnlyDictionary<string, string> tokens = null)
				: base(internalCorrId, correlationId, envelope, requireLeader, user, tokens) {
				Ensure.Nonnegative(transactionId, "transactionId");
				TransactionId = transactionId;
			}
		}

		public class TransactionCommitCompleted : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly long TransactionId;
			public readonly OperationResult Result;
			public readonly string Message;
			public readonly long FirstEventNumber;
			public readonly long LastEventNumber;
			public readonly long PreparePosition;
			public readonly long CommitPosition;

			public TransactionCommitCompleted(Guid correlationId, long transactionId, long firstEventNumber,
				long lastEventNumber, long preparePosition, long commitPosition) {
				if (firstEventNumber < -1)
					throw new ArgumentOutOfRangeException("firstEventNumber",
						String.Format("FirstEventNumber: {0}", firstEventNumber));
				if (lastEventNumber - firstEventNumber + 1 < 0)
					throw new ArgumentOutOfRangeException("lastEventNumber",
						String.Format("LastEventNumber {0}, FirstEventNumber {1}.", lastEventNumber, firstEventNumber));
				CorrelationId = correlationId;
				TransactionId = transactionId;
				Result = OperationResult.Success;
				Message = String.Empty;
				FirstEventNumber = firstEventNumber;
				LastEventNumber = lastEventNumber;
				PreparePosition = preparePosition;
				CommitPosition = commitPosition;
			}

			public TransactionCommitCompleted(Guid correlationId, long transactionId, OperationResult result,
				string message) {
				if (result == OperationResult.Success)
					throw new ArgumentException("Invalid constructor used for successful write.", "result");

				CorrelationId = correlationId;
				TransactionId = transactionId;
				Result = result;
				Message = message;
				FirstEventNumber = EventNumber.Invalid;
				LastEventNumber = EventNumber.Invalid;
			}

			private TransactionCommitCompleted(Guid correlationId, long transactionId, OperationResult result,
				string message,
				long firstEventNumber, long lastEventNumber) {
				CorrelationId = correlationId;
				TransactionId = transactionId;
				Result = result;
				Message = message;
				FirstEventNumber = firstEventNumber;
				LastEventNumber = lastEventNumber;
			}

			public TransactionCommitCompleted WithCorrelationId(Guid newCorrId) {
				return new TransactionCommitCompleted(newCorrId, TransactionId, Result, Message, FirstEventNumber,
					LastEventNumber);
			}
		}

		public class DeleteStream : WriteRequestMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly string EventStreamId;
			public readonly long ExpectedVersion;
			public readonly bool HardDelete;
			public readonly CancellationToken CancellationToken;

			public DeleteStream(Guid internalCorrId, Guid correlationId, IEnvelope envelope, bool requireLeader,
				string eventStreamId, long expectedVersion, bool hardDelete, ClaimsPrincipal user,
				IReadOnlyDictionary<string, string> tokens = null, CancellationToken cancellationToken = default)
				: base(internalCorrId, correlationId, envelope, requireLeader, user, tokens) {
				Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
				if (expectedVersion < Data.ExpectedVersion.Any)
					throw new ArgumentOutOfRangeException(nameof(expectedVersion));

				EventStreamId = eventStreamId;
				ExpectedVersion = expectedVersion;
				HardDelete = hardDelete;
				CancellationToken = cancellationToken;
			}
		}

		public class DeleteStreamCompleted : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly OperationResult Result;
			public readonly string Message;
			public readonly long PreparePosition;
			public readonly long CommitPosition;

			public DeleteStreamCompleted(Guid correlationId, OperationResult result, string message,
				long preparePosition, long commitPosition) {
				CorrelationId = correlationId;
				Result = result;
				Message = message;
				PreparePosition = preparePosition;
				CommitPosition = commitPosition;
			}

			public DeleteStreamCompleted(Guid correlationId, OperationResult result, string message) : this(
				correlationId, result, message, -1, -1) {
			}


			public DeleteStreamCompleted WithCorrelationId(Guid newCorrId) {
				return new DeleteStreamCompleted(newCorrId, Result, Message, PreparePosition, CommitPosition);
			}
		}

		public class ReadEvent : ReadRequestMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly string EventStreamId;
			public readonly long EventNumber;
			public readonly bool ResolveLinkTos;
			public readonly bool RequireLeader;

			public ReadEvent(Guid internalCorrId, Guid correlationId, IEnvelope envelope, string eventStreamId,
				long eventNumber,
				bool resolveLinkTos, bool requireLeader, ClaimsPrincipal user, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
				if (eventNumber < -1) throw new ArgumentOutOfRangeException(nameof(eventNumber));

				EventStreamId = eventStreamId;
				EventNumber = eventNumber;
				ResolveLinkTos = resolveLinkTos;
				RequireLeader = requireLeader;
			}
		}

		public class ReadEventCompleted : ReadResponseMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly string EventStreamId;
			public readonly ReadEventResult Result;
			public readonly ResolvedEvent Record;
			public readonly StreamMetadata StreamMetadata;
			public readonly bool IsCachePublic;
			public readonly string Error;

			public ReadEventCompleted(Guid correlationId, string eventStreamId, ReadEventResult result,
				ResolvedEvent record, StreamMetadata streamMetadata, bool isCachePublic, string error) {
				Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
				//if (result == ReadEventResult.Success)
				//    Ensure.NotNull(record.Event, "record.Event");

				CorrelationId = correlationId;
				EventStreamId = eventStreamId;
				Result = result;
				Record = record;
				StreamMetadata = streamMetadata;
				IsCachePublic = isCachePublic;
				Error = error;
			}
		}

		public class ReadStreamEventsForward : ReadRequestMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly string EventStreamId;
			public readonly long FromEventNumber;
			public readonly int MaxCount;
			public readonly bool ResolveLinkTos;
			public readonly bool RequireLeader;

			public readonly long? ValidationStreamVersion;
			public readonly TimeSpan? LongPollTimeout;

			public ReadStreamEventsForward(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				string eventStreamId, long fromEventNumber, int maxCount, bool resolveLinkTos,
				bool requireLeader, long? validationStreamVersion, ClaimsPrincipal user,
				TimeSpan? longPollTimeout = null, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
				if (fromEventNumber < -1) throw new ArgumentOutOfRangeException(nameof(fromEventNumber));

				EventStreamId = eventStreamId;
				FromEventNumber = fromEventNumber;
				MaxCount = maxCount;
				ResolveLinkTos = resolveLinkTos;
				RequireLeader = requireLeader;
				ValidationStreamVersion = validationStreamVersion;
				LongPollTimeout = longPollTimeout;
			}

			public override string ToString() {
				return String.Format(GetType().Name + " InternalCorrId: {0}, CorrelationId: {1}, EventStreamId: {2}, "
				                                    + "FromEventNumber: {3}, MaxCount: {4}, ResolveLinkTos: {5}, RequireLeader: {6}, ValidationStreamVersion: {7}",
					InternalCorrId, CorrelationId, EventStreamId,
					FromEventNumber, MaxCount, ResolveLinkTos, RequireLeader, ValidationStreamVersion);
			}
		}

		public class ReadStreamEventsForwardCompleted : ReadResponseMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly string EventStreamId;
			public readonly long FromEventNumber;
			public readonly int MaxCount;

			public readonly ReadStreamResult Result;
			public readonly ResolvedEvent[] Events;
			public readonly StreamMetadata StreamMetadata;
			public readonly bool IsCachePublic;
			public readonly string Error;
			public readonly long NextEventNumber;
			public readonly long LastEventNumber;
			public readonly bool IsEndOfStream;
			public readonly long TfLastCommitPosition;

			public ReadStreamEventsForwardCompleted(Guid correlationId, string eventStreamId, long fromEventNumber,
				int maxCount,
				ReadStreamResult result, ResolvedEvent[] events,
				StreamMetadata streamMetadata, bool isCachePublic,
				string error, long nextEventNumber, long lastEventNumber, bool isEndOfStream,
				long tfLastCommitPosition) {
				Ensure.NotNull(events, "events");

				if (result != ReadStreamResult.Success) {
					Ensure.Equal(nextEventNumber, -1, "nextEventNumber");
					Ensure.Equal(isEndOfStream, true, "isEndOfStream");
				}

				CorrelationId = correlationId;
				EventStreamId = eventStreamId;
				FromEventNumber = fromEventNumber;
				MaxCount = maxCount;

				Result = result;
				Events = events;
				StreamMetadata = streamMetadata;
				IsCachePublic = isCachePublic;
				Error = error;
				NextEventNumber = nextEventNumber;
				LastEventNumber = lastEventNumber;
				IsEndOfStream = isEndOfStream;
				TfLastCommitPosition = tfLastCommitPosition;
			}
		}

		public class ReadStreamEventsBackward : ReadRequestMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly string EventStreamId;
			public readonly long FromEventNumber;
			public readonly int MaxCount;
			public readonly bool ResolveLinkTos;
			public readonly bool RequireLeader;

			public readonly long? ValidationStreamVersion;

			public ReadStreamEventsBackward(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				string eventStreamId, long fromEventNumber, int maxCount, bool resolveLinkTos,
				bool requireLeader, long? validationStreamVersion, ClaimsPrincipal user, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
				if (fromEventNumber < -1) throw new ArgumentOutOfRangeException(nameof(fromEventNumber));

				EventStreamId = eventStreamId;
				FromEventNumber = fromEventNumber;
				MaxCount = maxCount;
				ResolveLinkTos = resolveLinkTos;
				RequireLeader = requireLeader;
				ValidationStreamVersion = validationStreamVersion;
			}

			public override string ToString() {
				return String.Format(GetType().Name + " InternalCorrId: {0}, CorrelationId: {1}, EventStreamId: {2}, "
				                                    + "FromEventNumber: {3}, MaxCount: {4}, ResolveLinkTos: {5}, RequireLeader: {6}, ValidationStreamVersion: {7}",
					InternalCorrId, CorrelationId, EventStreamId, FromEventNumber, MaxCount,
					ResolveLinkTos, RequireLeader, ValidationStreamVersion);
			}
		}

		public class ReadStreamEventsBackwardCompleted : ReadResponseMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly string EventStreamId;
			public readonly long FromEventNumber;
			public readonly int MaxCount;

			public readonly ReadStreamResult Result;
			public readonly ResolvedEvent[] Events;
			public readonly StreamMetadata StreamMetadata;
			public readonly bool IsCachePublic;
			public readonly string Error;
			public readonly long NextEventNumber;
			public readonly long LastEventNumber;
			public readonly bool IsEndOfStream;
			public readonly long TfLastCommitPosition;

			public ReadStreamEventsBackwardCompleted(Guid correlationId,
				string eventStreamId,
				long fromEventNumber,
				int maxCount,
				ReadStreamResult result,
				ResolvedEvent[] events,
				StreamMetadata streamMetadata,
				bool isCachePublic,
				string error,
				long nextEventNumber,
				long lastEventNumber,
				bool isEndOfStream,
				long tfLastCommitPosition) {
				Ensure.NotNull(events, "events");

				if (result != ReadStreamResult.Success) {
					Ensure.Equal(nextEventNumber, -1, "nextEventNumber");
					Ensure.Equal(isEndOfStream, true, "isEndOfStream");
				}

				CorrelationId = correlationId;
				EventStreamId = eventStreamId;
				FromEventNumber = fromEventNumber;
				MaxCount = maxCount;

				Result = result;
				Events = events;
				StreamMetadata = streamMetadata;
				IsCachePublic = isCachePublic;
				Error = error;
				NextEventNumber = nextEventNumber;
				LastEventNumber = lastEventNumber;
				IsEndOfStream = isEndOfStream;
				TfLastCommitPosition = tfLastCommitPosition;
			}
		}

		public class ReadAllEventsForward : ReadRequestMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly long CommitPosition;
			public readonly long PreparePosition;
			public readonly int MaxCount;
			public readonly bool ResolveLinkTos;
			public readonly bool RequireLeader;

			public readonly long? ValidationTfLastCommitPosition;
			public readonly TimeSpan? LongPollTimeout;

			public ReadAllEventsForward(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				long commitPosition, long preparePosition, int maxCount, bool resolveLinkTos,
				bool requireLeader, long? validationTfLastCommitPosition, ClaimsPrincipal user,
				TimeSpan? longPollTimeout = null, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				CommitPosition = commitPosition;
				PreparePosition = preparePosition;
				MaxCount = maxCount;
				ResolveLinkTos = resolveLinkTos;
				RequireLeader = requireLeader;
				ValidationTfLastCommitPosition = validationTfLastCommitPosition;
				LongPollTimeout = longPollTimeout;
			}
		}

		public class ReadAllEventsForwardCompleted : ReadResponseMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;

			public readonly ReadAllResult Result;
			public readonly string Error;

			public readonly ResolvedEvent[] Events;
			public readonly StreamMetadata StreamMetadata;
			public readonly bool IsCachePublic;
			public readonly int MaxCount;
			public readonly TFPos CurrentPos;
			public readonly TFPos NextPos;
			public readonly TFPos PrevPos;
			public readonly long TfLastCommitPosition;

			public bool IsEndOfStream {
				get { return Events == null || Events.Length < MaxCount; }
			}

			public ReadAllEventsForwardCompleted(Guid correlationId, ReadAllResult result, string error,
				ResolvedEvent[] events,
				StreamMetadata streamMetadata, bool isCachePublic, int maxCount,
				TFPos currentPos, TFPos nextPos, TFPos prevPos, long tfLastCommitPosition) {
				Ensure.NotNull(events, "events");

				CorrelationId = correlationId;
				Result = result;
				Error = error;
				Events = events;
				StreamMetadata = streamMetadata;
				IsCachePublic = isCachePublic;
				MaxCount = maxCount;
				CurrentPos = currentPos;
				NextPos = nextPos;
				PrevPos = prevPos;
				TfLastCommitPosition = tfLastCommitPosition;
			}
		}

		public class ReadAllEventsBackward : ReadRequestMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly long CommitPosition;
			public readonly long PreparePosition;
			public readonly int MaxCount;
			public readonly bool ResolveLinkTos;
			public readonly bool RequireLeader;

			public readonly long? ValidationTfLastCommitPosition;

			public ReadAllEventsBackward(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				long commitPosition, long preparePosition, int maxCount, bool resolveLinkTos,
				bool requireLeader, long? validationTfLastCommitPosition, ClaimsPrincipal user,
				DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				CommitPosition = commitPosition;
				PreparePosition = preparePosition;
				MaxCount = maxCount;
				ResolveLinkTos = resolveLinkTos;
				RequireLeader = requireLeader;
				ValidationTfLastCommitPosition = validationTfLastCommitPosition;
			}
		}

		public class ReadAllEventsBackwardCompleted : ReadResponseMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;

			public readonly ReadAllResult Result;
			public readonly string Error;

			public readonly ResolvedEvent[] Events;
			public readonly StreamMetadata StreamMetadata;
			public readonly bool IsCachePublic;
			public readonly int MaxCount;
			public readonly TFPos CurrentPos;
			public readonly TFPos NextPos;
			public readonly TFPos PrevPos;
			public readonly long TfLastCommitPosition;

			public bool IsEndOfStream {
				get { return Events == null || Events.Length < MaxCount; }
			}

			public ReadAllEventsBackwardCompleted(Guid correlationId, ReadAllResult result, string error,
				ResolvedEvent[] events,
				StreamMetadata streamMetadata, bool isCachePublic, int maxCount,
				TFPos currentPos, TFPos nextPos, TFPos prevPos, long tfLastCommitPosition) {
				Ensure.NotNull(events, "events");

				CorrelationId = correlationId;
				Result = result;
				Error = error;
				Events = events;
				StreamMetadata = streamMetadata;
				IsCachePublic = isCachePublic;
				MaxCount = maxCount;
				CurrentPos = currentPos;
				NextPos = nextPos;
				PrevPos = prevPos;
				TfLastCommitPosition = tfLastCommitPosition;
			}
		}

		public class FilteredReadAllEventsForward : ReadRequestMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly long CommitPosition;
			public readonly long PreparePosition;
			public readonly int MaxCount;
			public readonly bool ResolveLinkTos;
			public readonly bool RequireLeader;
			public readonly int MaxSearchWindow;
			public readonly IEventFilter EventFilter;

			public readonly long? ValidationTfLastCommitPosition;
			public readonly TimeSpan? LongPollTimeout;

			public FilteredReadAllEventsForward(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				long commitPosition, long preparePosition, int maxCount, bool resolveLinkTos, bool requireLeader,
				int maxSearchWindow, long? validationTfLastCommitPosition, IEventFilter eventFilter, ClaimsPrincipal user,
				TimeSpan? longPollTimeout = null, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				CommitPosition = commitPosition;
				PreparePosition = preparePosition;
				MaxCount = maxCount;
				ResolveLinkTos = resolveLinkTos;
				RequireLeader = requireLeader;
				ValidationTfLastCommitPosition = validationTfLastCommitPosition;
				LongPollTimeout = longPollTimeout;
				MaxSearchWindow = maxSearchWindow;
				EventFilter = eventFilter;
			}
		}

		public class FilteredReadAllEventsForwardCompleted : ReadResponseMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;

			public readonly FilteredReadAllResult Result;
			public readonly string Error;

			public readonly ResolvedEvent[] Events;
			public readonly StreamMetadata StreamMetadata;
			public readonly bool IsCachePublic;
			public readonly int MaxCount;
			public readonly TFPos CurrentPos;
			public readonly TFPos NextPos;
			public readonly TFPos PrevPos;
			public readonly long TfLastCommitPosition;
			public readonly bool IsEndOfStream;

			public FilteredReadAllEventsForwardCompleted(Guid correlationId, FilteredReadAllResult result, string error,
				ResolvedEvent[] events,
				StreamMetadata streamMetadata, bool isCachePublic, int maxCount,
				TFPos currentPos, TFPos nextPos, TFPos prevPos, long tfLastCommitPosition,
				bool isEndOfStream) {
				Ensure.NotNull(events, "events");

				CorrelationId = correlationId;
				Result = result;
				Error = error;
				Events = events;
				StreamMetadata = streamMetadata;
				IsCachePublic = isCachePublic;
				MaxCount = maxCount;
				CurrentPos = currentPos;
				NextPos = nextPos;
				PrevPos = prevPos;
				TfLastCommitPosition = tfLastCommitPosition;
				IsEndOfStream = isEndOfStream;
			}
		}

		public class FilteredReadAllEventsBackward : ReadRequestMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly long CommitPosition;
			public readonly long PreparePosition;
			public readonly int MaxCount;
			public readonly bool ResolveLinkTos;
			public readonly bool RequireLeader;
			public readonly int MaxSearchWindow;
			public readonly IEventFilter EventFilter;

			public readonly long? ValidationTfLastCommitPosition;
			public readonly TimeSpan? LongPollTimeout;

			public FilteredReadAllEventsBackward(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				long commitPosition, long preparePosition, int maxCount, bool resolveLinkTos, bool requireLeader,
				int maxSearchWindow, long? validationTfLastCommitPosition, IEventFilter eventFilter, ClaimsPrincipal user,
				TimeSpan? longPollTimeout = null, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				CommitPosition = commitPosition;
				PreparePosition = preparePosition;
				MaxCount = maxCount;
				ResolveLinkTos = resolveLinkTos;
				RequireLeader = requireLeader;
				ValidationTfLastCommitPosition = validationTfLastCommitPosition;
				LongPollTimeout = longPollTimeout;
				MaxSearchWindow = maxSearchWindow;
				EventFilter = eventFilter;
			}
		}

		public class FilteredReadAllEventsBackwardCompleted : ReadResponseMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;

			public readonly FilteredReadAllResult Result;
			public readonly string Error;

			public readonly ResolvedEvent[] Events;
			public readonly StreamMetadata StreamMetadata;
			public readonly bool IsCachePublic;
			public readonly int MaxCount;
			public readonly TFPos CurrentPos;
			public readonly TFPos NextPos;
			public readonly TFPos PrevPos;
			public readonly long TfLastCommitPosition;
			public readonly bool IsEndOfStream;

			public FilteredReadAllEventsBackwardCompleted(Guid correlationId, FilteredReadAllResult result, string error,
				ResolvedEvent[] events,
				StreamMetadata streamMetadata, bool isCachePublic, int maxCount,
				TFPos currentPos, TFPos nextPos, TFPos prevPos, long tfLastCommitPosition,
				bool isEndOfStream) {
				Ensure.NotNull(events, "events");

				CorrelationId = correlationId;
				Result = result;
				Error = error;
				Events = events;
				StreamMetadata = streamMetadata;
				IsCachePublic = isCachePublic;
				MaxCount = maxCount;
				CurrentPos = currentPos;
				NextPos = nextPos;
				PrevPos = prevPos;
				TfLastCommitPosition = tfLastCommitPosition;
				IsEndOfStream = isEndOfStream;
			}
		}

		//Persistent subscriptions
		public class ConnectToPersistentSubscription : ReadRequestMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
			public override int MsgTypeId { get { return TypeId; } }

			public readonly Guid ConnectionId;
			public readonly string ConnectionName;
			public readonly string SubscriptionId;
			public readonly string EventStreamId;
			public readonly int AllowedInFlightMessages;
			public readonly string From;

			public ConnectToPersistentSubscription(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				Guid connectionId, string connectionName, string subscriptionId, string eventStreamId,
				int allowedInFlightMessages, string from, ClaimsPrincipal user, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				Ensure.NotEmptyGuid(connectionId, "connectionId");
				Ensure.NotNullOrEmpty(subscriptionId, "subscriptionId");
				Ensure.Nonnegative(allowedInFlightMessages, "AllowedInFlightMessages");
				SubscriptionId = subscriptionId;
				ConnectionId = connectionId;
				ConnectionName = connectionName;
				AllowedInFlightMessages = allowedInFlightMessages;
				EventStreamId = eventStreamId;
				From = from;
			}
		}

		public class CreatePersistentSubscription : ReadRequestMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly long StartFrom;
			public readonly int MessageTimeoutMilliseconds;
			public readonly bool RecordStatistics;

			public readonly bool ResolveLinkTos;
			public readonly int MaxRetryCount;
			public readonly int BufferSize;
			public readonly int LiveBufferSize;
			public readonly int ReadBatchSize;

			public readonly string GroupName;
			public readonly string EventStreamId;
			public readonly int MaxSubscriberCount;
			public readonly string NamedConsumerStrategy;
			public readonly int MaxCheckPointCount;
			public readonly int MinCheckPointCount;
			public readonly int CheckPointAfterMilliseconds;

			public CreatePersistentSubscription(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				string eventStreamId, string groupName, bool resolveLinkTos, long startFrom,
				int messageTimeoutMilliseconds, bool recordStatistics, int maxRetryCount, int bufferSize,
				int liveBufferSize, int readbatchSize,
				int checkPointAfterMilliseconds, int minCheckPointCount, int maxCheckPointCount,
				int maxSubscriberCount, string namedConsumerStrategy, ClaimsPrincipal user, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				ResolveLinkTos = resolveLinkTos;
				EventStreamId = eventStreamId;
				GroupName = groupName;
				StartFrom = startFrom;
				MessageTimeoutMilliseconds = messageTimeoutMilliseconds;
				RecordStatistics = recordStatistics;
				MaxRetryCount = maxRetryCount;
				BufferSize = bufferSize;
				LiveBufferSize = liveBufferSize;
				ReadBatchSize = readbatchSize;
				MaxCheckPointCount = maxCheckPointCount;
				MinCheckPointCount = minCheckPointCount;
				CheckPointAfterMilliseconds = checkPointAfterMilliseconds;
				MaxSubscriberCount = maxSubscriberCount;
				NamedConsumerStrategy = namedConsumerStrategy;
			}
		}

		public class CreatePersistentSubscriptionCompleted : ReadResponseMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly string Reason;
			public readonly CreatePersistentSubscriptionResult Result;

			public CreatePersistentSubscriptionCompleted(Guid correlationId, CreatePersistentSubscriptionResult result,
				string reason) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				CorrelationId = correlationId;
				Result = result;
				Reason = reason;
			}

			public enum CreatePersistentSubscriptionResult {
				Success = 0,
				AlreadyExists = 1,
				Fail = 2,
				AccessDenied = 3
			}
		}

		public class UpdatePersistentSubscription : ReadRequestMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly long StartFrom;
			public readonly int MessageTimeoutMilliseconds;
			public readonly bool RecordStatistics;

			public readonly bool ResolveLinkTos;
			public readonly int MaxRetryCount;
			public readonly int BufferSize;
			public readonly int LiveBufferSize;
			public readonly int ReadBatchSize;

			public readonly string GroupName;
			public readonly string EventStreamId;
			public readonly int MaxSubscriberCount;

			public readonly int MaxCheckPointCount;
			public readonly int MinCheckPointCount;
			public readonly int CheckPointAfterMilliseconds;
			public readonly string NamedConsumerStrategy;

			public UpdatePersistentSubscription(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				string eventStreamId, string groupName, bool resolveLinkTos, long startFrom,
				int messageTimeoutMilliseconds, bool recordStatistics, int maxRetryCount, int bufferSize,
				int liveBufferSize, int readbatchSize, int checkPointAfterMilliseconds, int minCheckPointCount,
				int maxCheckPointCount, int maxSubscriberCount, string namedConsumerStrategy, ClaimsPrincipal user,
				DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				ResolveLinkTos = resolveLinkTos;
				EventStreamId = eventStreamId;
				GroupName = groupName;
				StartFrom = startFrom;
				MessageTimeoutMilliseconds = messageTimeoutMilliseconds;
				RecordStatistics = recordStatistics;
				MaxRetryCount = maxRetryCount;
				BufferSize = bufferSize;
				LiveBufferSize = liveBufferSize;
				ReadBatchSize = readbatchSize;
				MaxCheckPointCount = maxCheckPointCount;
				MinCheckPointCount = minCheckPointCount;
				CheckPointAfterMilliseconds = checkPointAfterMilliseconds;
				MaxSubscriberCount = maxSubscriberCount;
				NamedConsumerStrategy = namedConsumerStrategy;
			}
		}

		public class UpdatePersistentSubscriptionCompleted : ReadResponseMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly string Reason;
			public readonly UpdatePersistentSubscriptionResult Result;

			public UpdatePersistentSubscriptionCompleted(Guid correlationId, UpdatePersistentSubscriptionResult result,
				string reason) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				CorrelationId = correlationId;
				Result = result;
				Reason = reason;
			}

			public enum UpdatePersistentSubscriptionResult {
				Success = 0,
				DoesNotExist = 1,
				Fail = 2,
				AccessDenied = 3
			}
		}

		public class ReadNextNPersistentMessages : ReadRequestMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly string GroupName;
			public readonly string EventStreamId;
			public readonly int Count;

			public ReadNextNPersistentMessages(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				string eventStreamId, string groupName, int count, ClaimsPrincipal user, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				GroupName = groupName;
				EventStreamId = eventStreamId;
				Count = count;
			}
		}

		public class ReadNextNPersistentMessagesCompleted : ReadResponseMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly string Reason;
			public readonly ReadNextNPersistentMessagesResult Result;
			public readonly (ResolvedEvent ResolvedEvent, int RetryCount)[] Events;

			public ReadNextNPersistentMessagesCompleted(Guid correlationId, ReadNextNPersistentMessagesResult result,
				string reason, (ResolvedEvent ResolvedEvent, int RetryCount)[] events) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				CorrelationId = correlationId;
				Result = result;
				Reason = reason;
				Events = events;
			}

			public enum ReadNextNPersistentMessagesResult {
				Success = 0,
				DoesNotExist = 1,
				Fail = 2,
				AccessDenied = 3
			}
		}

		public class DeletePersistentSubscription : ReadRequestMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly string GroupName;
			public readonly string EventStreamId;

			public DeletePersistentSubscription(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				string eventStreamId, string groupName, ClaimsPrincipal user, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				GroupName = groupName;
				EventStreamId = eventStreamId;
			}
		}

		public class DeletePersistentSubscriptionCompleted : ReadResponseMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly string Reason;
			public readonly DeletePersistentSubscriptionResult Result;

			public DeletePersistentSubscriptionCompleted(Guid correlationId, DeletePersistentSubscriptionResult result,
				string reason) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				CorrelationId = correlationId;
				Result = result;
				Reason = reason;
			}

			public enum DeletePersistentSubscriptionResult {
				Success = 0,
				DoesNotExist = 1,
				Fail = 2,
				AccessDenied = 3
			}
		}

		public class PersistentSubscriptionAckEvents : ReadRequestMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly string SubscriptionId;
			public readonly Guid[] ProcessedEventIds;

			public PersistentSubscriptionAckEvents(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				string subscriptionId, Guid[] processedEventIds, ClaimsPrincipal user, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				Ensure.NotNullOrEmpty(subscriptionId, "subscriptionId");
				Ensure.NotNull(processedEventIds, "processedEventIds");

				SubscriptionId = subscriptionId;
				ProcessedEventIds = processedEventIds;
			}
		}

		public class PersistentSubscriptionNackEvents : ReadRequestMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly string SubscriptionId;
			public readonly Guid[] ProcessedEventIds;
			public readonly string Message;
			public readonly NakAction Action;

			public PersistentSubscriptionNackEvents(Guid internalCorrId,
				Guid correlationId,
				IEnvelope envelope,
				string subscriptionId,
				string message,
				NakAction action,
				Guid[] processedEventIds,
				ClaimsPrincipal user,
				DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				SubscriptionId = subscriptionId;
				ProcessedEventIds = processedEventIds;
				Message = message;
				Action = action;
			}

			public enum NakAction {
				Unknown = 0,
				Park = 1,
				Retry = 2,
				Skip = 3,
				Stop = 4
			}
		}

		public class PersistentSubscriptionNakEvents : ReadRequestMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly string SubscriptionId;
			public readonly Guid[] ProcessedEventIds;

			public PersistentSubscriptionNakEvents(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				string subscriptionId, Guid[] processedEventIds, ClaimsPrincipal user, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				Ensure.NotNullOrEmpty(subscriptionId, "subscriptionId");
				Ensure.NotNull(processedEventIds, "processedEventIds");

				SubscriptionId = subscriptionId;
				ProcessedEventIds = processedEventIds;
			}
		}

		public class PersistentSubscriptionConfirmation : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly long LastIndexedPosition;
			public readonly long? LastEventNumber;
			public readonly string SubscriptionId;

			public PersistentSubscriptionConfirmation(string subscriptionId, Guid correlationId,
				long lastIndexedPosition, long? lastEventNumber) {
				CorrelationId = correlationId;
				LastIndexedPosition = lastIndexedPosition;
				LastEventNumber = lastEventNumber;
				SubscriptionId = subscriptionId;
			}
		}

		public class ReplayParkedMessages : ReadRequestMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly string EventStreamId;
			public readonly string GroupName;
			public readonly long? StopAt;

			public ReplayParkedMessages(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				string eventStreamId, string groupName, long? stopAt, ClaimsPrincipal user, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				EventStreamId = eventStreamId;
				GroupName = groupName;
				StopAt = stopAt;
			}
		}

		public class ReplayParkedMessage : ReadRequestMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly string EventStreamId;
			public readonly string GroupName;
			public readonly ResolvedEvent Event;

			public ReplayParkedMessage(Guid internalCorrId, Guid correlationId, IEnvelope envelope, string streamId,
				string groupName, ResolvedEvent @event, ClaimsPrincipal user, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				EventStreamId = streamId;
				GroupName = groupName;
				Event = @event;
			}
		}

		public class ReplayMessagesReceived : ReadResponseMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly string Reason;
			public readonly ReplayMessagesReceivedResult Result;

			public ReplayMessagesReceived(Guid correlationId, ReplayMessagesReceivedResult result, string reason) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				CorrelationId = correlationId;
				Result = result;
				Reason = reason;
			}

			public enum ReplayMessagesReceivedResult {
				Success = 0,
				DoesNotExist = 1,
				Fail = 2,
				AccessDenied = 3
			}
		}

		//End of persistence subscriptions


		public class SubscribeToStream : ReadRequestMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid ConnectionId;
			public readonly string EventStreamId; // should be empty to subscribe to all
			public readonly bool ResolveLinkTos;

			public SubscribeToStream(Guid internalCorrId, Guid correlationId, IEnvelope envelope, Guid connectionId,
				string eventStreamId, bool resolveLinkTos, ClaimsPrincipal user, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				Ensure.NotEmptyGuid(connectionId, "connectionId");
				ConnectionId = connectionId;
				EventStreamId = eventStreamId;
				ResolveLinkTos = resolveLinkTos;
			}
		}

		public class FilteredSubscribeToStream : ReadRequestMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid ConnectionId;
			public readonly string EventStreamId; // should be empty to subscribe to all
			public readonly bool ResolveLinkTos;
			public readonly IEventFilter EventFilter;
			public readonly int CheckpointInterval;

			public FilteredSubscribeToStream(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				Guid connectionId, string eventStreamId, bool resolveLinkTos, ClaimsPrincipal user,
				IEventFilter eventFilter, int checkpointInterval, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				Ensure.NotEmptyGuid(connectionId, "connectionId");
				ConnectionId = connectionId;
				EventStreamId = eventStreamId;
				ResolveLinkTos = resolveLinkTos;
				EventFilter = eventFilter;
				CheckpointInterval = checkpointInterval;
			}
		}

		public class CheckpointReached : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly TFPos? Position;

			public CheckpointReached(Guid correlationId, TFPos? position) {
				CorrelationId = correlationId;
				Position = position;
			}
		}

		public class UnsubscribeFromStream : ReadRequestMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public UnsubscribeFromStream(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				ClaimsPrincipal user, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
			}
		}

		public class SubscriptionConfirmation : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly long LastIndexedPosition;
			public readonly long? LastEventNumber;

			public SubscriptionConfirmation(Guid correlationId, long lastIndexedPosition, long? lastEventNumber) {
				CorrelationId = correlationId;
				LastIndexedPosition = lastIndexedPosition;
				LastEventNumber = lastEventNumber;
			}
		}

		public class StreamEventAppeared : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly ResolvedEvent Event;

			public StreamEventAppeared(Guid correlationId, ResolvedEvent @event) {
				CorrelationId = correlationId;
				Event = @event;
			}
		}

		public class PersistentSubscriptionStreamEventAppeared : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly ResolvedEvent Event;
			public readonly int RetryCount;

			public PersistentSubscriptionStreamEventAppeared(Guid correlationId, ResolvedEvent @event, int retryCount) {
				CorrelationId = correlationId;
				Event = @event;
				RetryCount = retryCount;
			}
		}

		public class SubscriptionDropped : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly SubscriptionDropReason Reason;

			public SubscriptionDropped(Guid correlationId, SubscriptionDropReason reason) {
				CorrelationId = correlationId;
				Reason = reason;
			}
		}

		public class MergeIndexes : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly IEnvelope Envelope;
			public readonly Guid CorrelationId;
			public readonly ClaimsPrincipal User;

			public MergeIndexes(IEnvelope envelope, Guid correlationId, ClaimsPrincipal user) {
				Ensure.NotNull(envelope, "envelope");
				Envelope = envelope;
				CorrelationId = correlationId;
				User = user;
			}
		}

		public class MergeIndexesResponse : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly MergeIndexesResult Result;

			public MergeIndexesResponse(Guid correlationId, MergeIndexesResult result) {
				CorrelationId = correlationId;
				Result = result;
			}

			public override string ToString() {
				return String.Format("Result: {0}", Result);
			}

			public enum MergeIndexesResult {
				Started
			}
		}

		public class SetNodePriority : Message
		{
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly int NodePriority;

			public SetNodePriority(int nodePriority) {
				NodePriority = nodePriority;
			}
		}

		public class ResignNode : Message
		{
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
		}

		public class ScavengeDatabase : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly IEnvelope Envelope;
			public readonly Guid CorrelationId;
			public readonly ClaimsPrincipal User;
			public readonly int StartFromChunk;
			public readonly int Threads;

			public ScavengeDatabase(IEnvelope envelope, Guid correlationId, ClaimsPrincipal user, int startFromChunk,
				int threads) {
				Ensure.NotNull(envelope, "envelope");
				Envelope = envelope;
				CorrelationId = correlationId;
				User = user;
				StartFromChunk = startFromChunk;
				Threads = threads;
			}
		}

		public class StopDatabaseScavenge : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly IEnvelope Envelope;
			public readonly Guid CorrelationId;
			public readonly ClaimsPrincipal User;
			public readonly string ScavengeId;

			public StopDatabaseScavenge(IEnvelope envelope, Guid correlationId, ClaimsPrincipal user, string scavengeId) {
				Ensure.NotNull(envelope, "envelope");
				Envelope = envelope;
				CorrelationId = correlationId;
				User = user;
				ScavengeId = scavengeId;
			}
		}

		public class ScavengeDatabaseResponse : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly ScavengeResult Result;
			public readonly string ScavengeId;

			public ScavengeDatabaseResponse(Guid correlationId,
				ScavengeResult result, string scavengeId) {
				CorrelationId = correlationId;
				Result = result;
				ScavengeId = scavengeId;
			}

			public override string ToString() {
				return String.Format("Result: {0}, ScavengeId: {1}", Result, ScavengeId);
			}

			public enum ScavengeResult {
				Started,
				Unauthorized,
				InProgress,
				Stopped,
				InvalidScavengeId
			}
		}

		public class IdentifyClient : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly int Version;
			public readonly string ConnectionName;

			public IdentifyClient(Guid correlationId,
				int version,
				string connectionName) {
				CorrelationId = correlationId;
				Version = version;
				ConnectionName = connectionName;
			}

			public override string ToString() {
				return String.Format("Version: {0}, Connection Name: {1}", Version, ConnectionName);
			}
		}

		public class ClientIdentified : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;

			public ClientIdentified(Guid correlationId) {
				CorrelationId = correlationId;
			}
		}
	}
}
