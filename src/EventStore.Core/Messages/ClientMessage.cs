// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Settings;
using FilteredReadAllResult = EventStore.Core.Data.FilteredReadAllResult;
using ReadStreamResult = EventStore.Core.Data.ReadStreamResult;

namespace EventStore.Core.Messages;

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

public static partial class ClientMessage {
	[DerivedMessage(CoreMessage.Client)]
	public partial class RequestShutdown(bool exitProcess, bool shutdownHttp) : Message {
		public readonly bool ExitProcess = exitProcess;
		public readonly bool ShutdownHttp = shutdownHttp;
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class ReloadConfig : Message;

	[DerivedMessage]
	public abstract partial class WriteRequestMessage(
		Guid internalCorrId,
		Guid correlationId,
		IEnvelope envelope,
		bool requireLeader,
		ClaimsPrincipal user,
		IReadOnlyDictionary<string, string> tokens)
		: Message {
		public readonly Guid InternalCorrId = Ensure.NotEmptyGuid(internalCorrId);
		public readonly Guid CorrelationId = Ensure.NotEmptyGuid(correlationId);
		public readonly IEnvelope Envelope = Ensure.NotNull(envelope);
		public readonly bool RequireLeader = requireLeader;

		public readonly ClaimsPrincipal User = user;
		public string Login => Tokens?.GetValueOrDefault("uid");
		public string Password => Tokens?.GetValueOrDefault("pwd");
		public readonly IReadOnlyDictionary<string, string> Tokens = tokens;
	}

	[DerivedMessage]
	public abstract partial class ReadRequestMessage(
		Guid internalCorrId,
		Guid correlationId,
		IEnvelope envelope,
		ClaimsPrincipal user,
		DateTime? expires,
		CancellationToken cancellationToken = default)
		: Message {
		public readonly Guid InternalCorrId = Ensure.NotEmptyGuid(internalCorrId);
		public readonly Guid CorrelationId = Ensure.NotEmptyGuid(correlationId);
		public readonly IEnvelope Envelope = Ensure.NotNull(envelope);

		public readonly ClaimsPrincipal User = user;

		public readonly DateTime Expires = expires ?? DateTime.UtcNow.AddMilliseconds(ESConsts.ReadRequestTimeout);
		public readonly CancellationToken CancellationToken = cancellationToken;

		public override string ToString() =>
			$"{GetType().Name} " +
			$"InternalCorrId: {InternalCorrId}, " +
			$"CorrelationId: {CorrelationId}, " +
			$"User: {User?.FindFirst(ClaimTypes.Name)?.Value ?? "(anonymous)"}, " +
			$"Envelope: {{ {Envelope} }}, " +
			$"Expires: {Expires}";
	}

	[DerivedMessage]
	public abstract partial class ReadResponseMessage : Message;

	[DerivedMessage(CoreMessage.Client)]
	public partial class TcpForwardMessage(Message message) : Message {
		public readonly Message Message = Ensure.NotNull(message);
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class NotHandled(
		Guid correlationId,
		NotHandled.Types.NotHandledReason reason,
		NotHandled.Types.LeaderInfo leaderInfo)
		: Message {
		public readonly Guid CorrelationId = correlationId;
		public readonly Types.NotHandledReason Reason = reason;
		public readonly Types.LeaderInfo LeaderInfo = leaderInfo;
		public readonly string Description;

		public NotHandled(Guid correlationId,
			Types.NotHandledReason reason,
			string description) : this(correlationId, reason, default(Types.LeaderInfo)) {
			Description = description;
		}

		public static class Types {
			public enum NotHandledReason {
				NotReady,
				TooBusy,
				NotLeader,
				IsReadOnly
			}

			public class LeaderInfo(EndPoint externalTcp, bool isSecure, EndPoint http) {
				public bool IsSecure { get; } = isSecure;
				public EndPoint ExternalTcp { get; } = externalTcp;
				public EndPoint Http { get; } = http;
			}
		}
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class WriteEvents : WriteRequestMessage {
		public readonly string EventStreamId;
		public readonly long ExpectedVersion;
		public readonly Event[] Events;
		public readonly CancellationToken CancellationToken;

		public WriteEvents(Guid internalCorrId, Guid correlationId, IEnvelope envelope, bool requireLeader,
			string eventStreamId, long expectedVersion, Event[] events, ClaimsPrincipal user,
			IReadOnlyDictionary<string, string> tokens = null, CancellationToken cancellationToken = default)
			: base(internalCorrId, correlationId, envelope, requireLeader, user, tokens) {
			if (expectedVersion is < Data.ExpectedVersion.StreamExists or Data.ExpectedVersion.Invalid)
				throw new ArgumentOutOfRangeException(nameof(expectedVersion));
			EventStreamId = Ensure.NotNullOrEmpty(eventStreamId);
			ExpectedVersion = expectedVersion;
			Events = Ensure.NotNull(events);
			CancellationToken = cancellationToken;
		}

		public WriteEvents(Guid internalCorrId, Guid correlationId, IEnvelope envelope, bool requireLeader,
			string eventStreamId, long expectedVersion, Event @event, ClaimsPrincipal user,
			IReadOnlyDictionary<string, string> tokens = null)
			: this(internalCorrId, correlationId, envelope, requireLeader, eventStreamId, expectedVersion,
				@event == null ? null : [@event], user, tokens) {
		}

		public override string ToString() =>
			$"WRITE: InternalCorrId: {InternalCorrId}, CorrelationId: {CorrelationId}, EventStreamId: {EventStreamId}, ExpectedVersion: {ExpectedVersion}, Events: {Events.Length}";
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class WriteEventsCompleted : Message {
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
				throw new ArgumentOutOfRangeException(nameof(firstEventNumber), $"FirstEventNumber: {firstEventNumber}");
			if (lastEventNumber - firstEventNumber + 1 < 0)
				throw new ArgumentOutOfRangeException(nameof(lastEventNumber), $"LastEventNumber {lastEventNumber}, FirstEventNumber {firstEventNumber}.");

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
			return new(newCorrId, Result, Message, FirstEventNumber, LastEventNumber, PreparePosition, CommitPosition, CurrentVersion);
		}

		public override string ToString() {
			return
				$"WRITE COMPLETED: CorrelationId: {CorrelationId}, Result: {Result}, Message: {Message}, FirstEventNumber: {FirstEventNumber}, LastEventNumber: {LastEventNumber}, CurrentVersion: {CurrentVersion}";
		}
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class TransactionStart : WriteRequestMessage {
		public readonly string EventStreamId;
		public readonly long ExpectedVersion;

		public TransactionStart(Guid internalCorrId, Guid correlationId, IEnvelope envelope, bool requireLeader,
			string eventStreamId, long expectedVersion, ClaimsPrincipal user,
			IReadOnlyDictionary<string, string> tokens = null)
			: base(internalCorrId, correlationId, envelope, requireLeader, user, tokens) {
			ArgumentOutOfRangeException.ThrowIfLessThan(expectedVersion, Data.ExpectedVersion.Any);
			EventStreamId = Ensure.NotNullOrEmpty(eventStreamId);
			ExpectedVersion = expectedVersion;
		}
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class TransactionStartCompleted(Guid correlationId, long transactionId, OperationResult result, string message) : Message {
		public readonly Guid CorrelationId = correlationId;
		public readonly long TransactionId = transactionId;
		public readonly OperationResult Result = result;
		public readonly string Message = message;

		public TransactionStartCompleted WithCorrelationId(Guid newCorrId) => new(newCorrId, TransactionId, Result, Message);
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class TransactionWrite(
		Guid internalCorrId,
		Guid correlationId,
		IEnvelope envelope,
		bool requireLeader,
		long transactionId,
		Event[] events,
		ClaimsPrincipal user,
		IReadOnlyDictionary<string, string> tokens = null)
		: WriteRequestMessage(internalCorrId, correlationId, envelope, requireLeader, user, tokens) {
		public readonly long TransactionId = Ensure.Nonnegative(transactionId);
		public readonly Event[] Events = Ensure.NotNull(events);
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class TransactionWriteCompleted(
		Guid correlationId,
		long transactionId,
		OperationResult result,
		string message)
		: Message {
		public readonly Guid CorrelationId = correlationId;
		public readonly long TransactionId = transactionId;
		public readonly OperationResult Result = result;
		public readonly string Message = message;

		public TransactionWriteCompleted WithCorrelationId(Guid newCorrId) => new(newCorrId, TransactionId, Result, Message);
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class TransactionCommit(
		Guid internalCorrId,
		Guid correlationId,
		IEnvelope envelope,
		bool requireLeader,
		long transactionId,
		ClaimsPrincipal user,
		IReadOnlyDictionary<string, string> tokens = null)
		: WriteRequestMessage(internalCorrId, correlationId, envelope, requireLeader, user, tokens) {
		public readonly long TransactionId = Ensure.Nonnegative(transactionId);
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class TransactionCommitCompleted : Message {
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
				throw new ArgumentOutOfRangeException(nameof(firstEventNumber), $"FirstEventNumber: {firstEventNumber}");
			if (lastEventNumber - firstEventNumber + 1 < 0)
				throw new ArgumentOutOfRangeException(nameof(lastEventNumber), $"LastEventNumber {lastEventNumber}, FirstEventNumber {firstEventNumber}.");
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
				throw new ArgumentException("Invalid constructor used for successful write.", nameof(result));

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
			return new(newCorrId, TransactionId, Result, Message, FirstEventNumber, LastEventNumber);
		}
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class DeleteStream(
		Guid internalCorrId,
		Guid correlationId,
		IEnvelope envelope,
		bool requireLeader,
		string eventStreamId,
		long expectedVersion,
		bool hardDelete,
		ClaimsPrincipal user,
		IReadOnlyDictionary<string, string> tokens = null,
		CancellationToken cancellationToken = default)
		: WriteRequestMessage(internalCorrId, correlationId, envelope, requireLeader, user, tokens) {
		public readonly string EventStreamId = Ensure.NotNullOrEmpty(eventStreamId);

		public readonly long ExpectedVersion = expectedVersion switch {
			Data.ExpectedVersion.Invalid => throw new ArgumentOutOfRangeException(nameof(expectedVersion)),
			< Data.ExpectedVersion.StreamExists => throw new ArgumentOutOfRangeException(nameof(expectedVersion)),
			_ => expectedVersion
		};

		public readonly bool HardDelete = hardDelete;
		public readonly CancellationToken CancellationToken = cancellationToken;
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class DeleteStreamCompleted : Message {
		public readonly Guid CorrelationId;
		public readonly OperationResult Result;
		public readonly string Message;
		public readonly long PreparePosition;
		public readonly long CommitPosition;
		public readonly long CurrentVersion;

		public DeleteStreamCompleted(Guid correlationId, OperationResult result, string message,
			long currentVersion, long preparePosition, long commitPosition) {
			CorrelationId = correlationId;
			Result = result;
			Message = message;
			CurrentVersion = currentVersion;
			PreparePosition = preparePosition;
			CommitPosition = commitPosition;
		}

		public DeleteStreamCompleted(Guid correlationId, OperationResult result, string message,
			long currentVersion = -1L) : this(correlationId, result, message, currentVersion, -1, -1) {
		}

		public DeleteStreamCompleted WithCorrelationId(Guid newCorrId) {
			return new(newCorrId, Result, Message, CurrentVersion, PreparePosition, CommitPosition);
		}
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class ReadLogEvents(
		Guid internalCorrId,
		Guid correlationId,
		IEnvelope envelope,
		long[] logPositions,
		ClaimsPrincipal user,
		DateTime? expires,
		CancellationToken cancellationToken = default)
		: ReadRequestMessage(internalCorrId, correlationId, envelope, user, expires, cancellationToken) {
		public long[] LogPositions = logPositions;
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class ReadLogEventsCompleted(Guid correlationId, ReadEventResult result, ResolvedEvent[] records, string error)
		: ReadResponseMessage {
		public readonly Guid CorrelationId = correlationId;
		public readonly ReadEventResult Result = result;
		public readonly ResolvedEvent[] Records = records;
		public readonly string Error = error;
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class ReadEvent : ReadRequestMessage {
		public readonly string EventStreamId;
		public readonly long EventNumber;
		public readonly bool ResolveLinkTos;
		public readonly bool RequireLeader;

		public ReadEvent(Guid internalCorrId, Guid correlationId, IEnvelope envelope, string eventStreamId,
			long eventNumber,
			bool resolveLinkTos, bool requireLeader, ClaimsPrincipal user, DateTime? expires = null,
			CancellationToken cancellationToken = default)
			: base(internalCorrId, correlationId, envelope, user, expires, cancellationToken) {
			ArgumentOutOfRangeException.ThrowIfLessThan(eventNumber, -1);
			EventStreamId = Ensure.NotNullOrEmpty(eventStreamId);
			EventNumber = eventNumber;
			ResolveLinkTos = resolveLinkTos;
			RequireLeader = requireLeader;
		}

		public override string ToString() =>
			$"{base.ToString()}, " +
			$"EventStreamId: {EventStreamId}, " +
			$"EventNumber: {EventNumber}, " +
			$"ResolveLinkTos: {ResolveLinkTos}, " +
			$"RequireLeader: {RequireLeader}";
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class ReadEventCompleted : ReadResponseMessage {
		public readonly Guid CorrelationId;
		public readonly string EventStreamId;
		public readonly ReadEventResult Result;
		public readonly ResolvedEvent Record;
		public readonly StreamMetadata StreamMetadata;
		public readonly bool IsCachePublic;
		public readonly string Error;

		public ReadEventCompleted(Guid correlationId, string eventStreamId, ReadEventResult result,
			ResolvedEvent record, StreamMetadata streamMetadata, bool isCachePublic, string error) {
			//if (result == ReadEventResult.Success)
			//    Ensure.NotNull(record.Event, "record.Event");
			CorrelationId = correlationId;
			EventStreamId = Ensure.NotNullOrEmpty(eventStreamId);
			Result = result;
			Record = record;
			StreamMetadata = streamMetadata;
			IsCachePublic = isCachePublic;
			Error = error;
		}
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class ReadStreamEventsForward : ReadRequestMessage {
		public readonly string EventStreamId;
		public readonly long FromEventNumber;
		public readonly int MaxCount;
		public readonly bool ResolveLinkTos;
		public readonly bool RequireLeader;

		public readonly long? ValidationStreamVersion;
		public readonly TimeSpan? LongPollTimeout;
		public readonly bool ReplyOnExpired;

		public ReadStreamEventsForward(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
			string eventStreamId, long fromEventNumber, int maxCount, bool resolveLinkTos,
			bool requireLeader, long? validationStreamVersion, ClaimsPrincipal user,
			bool replyOnExpired,
			TimeSpan? longPollTimeout = null, DateTime? expires = null,
			CancellationToken cancellationToken = default)
			: base(internalCorrId, correlationId, envelope, user, expires, cancellationToken) {
			ArgumentOutOfRangeException.ThrowIfLessThan(fromEventNumber, -1);
			EventStreamId = Ensure.NotNullOrEmpty(eventStreamId);
			FromEventNumber = fromEventNumber;
			MaxCount = maxCount;
			ResolveLinkTos = resolveLinkTos;
			RequireLeader = requireLeader;
			ValidationStreamVersion = validationStreamVersion;
			LongPollTimeout = longPollTimeout;
			ReplyOnExpired = replyOnExpired;
		}

		public override string ToString() =>
			$"{base.ToString()}, " +
			$"EventStreamId: {EventStreamId}, " +
			$"FromEventNumber: {FromEventNumber}, " +
			$"MaxCount: {MaxCount}, " +
			$"ResolveLinkTos: {ResolveLinkTos}, " +
			$"RequireLeader: {RequireLeader}, " +
			$"ValidationStreamVersion: {ValidationStreamVersion}, " +
			$"LongPollTimeout: {LongPollTimeout}, " +
			$"ReplyOnExpired: {ReplyOnExpired}";
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class ReadStreamEventsForwardCompleted : ReadResponseMessage {
		public readonly Guid CorrelationId;
		public readonly string EventStreamId;
		public readonly long FromEventNumber;
		public readonly int MaxCount;

		public readonly ReadStreamResult Result;
		public readonly IReadOnlyList<ResolvedEvent> Events;
		public readonly StreamMetadata StreamMetadata;
		public readonly bool IsCachePublic;
		public readonly string Error;
		public readonly long NextEventNumber;
		public readonly long LastEventNumber;
		public readonly bool IsEndOfStream;
		public readonly long TfLastCommitPosition;

		public ReadStreamEventsForwardCompleted(Guid correlationId, string eventStreamId, long fromEventNumber,
			int maxCount, ReadStreamResult result, IReadOnlyList<ResolvedEvent> events,
			StreamMetadata streamMetadata, bool isCachePublic,
			string error, long nextEventNumber, long lastEventNumber, bool isEndOfStream,
			long tfLastCommitPosition) {
			if (result != ReadStreamResult.Success && result != ReadStreamResult.Expired) {
				Ensure.Equal(nextEventNumber, -1, "nextEventNumber");
				Ensure.Equal(isEndOfStream, true, "isEndOfStream");
			}

			CorrelationId = correlationId;
			EventStreamId = eventStreamId;
			FromEventNumber = fromEventNumber;
			MaxCount = maxCount;

			Result = result;
			Events = Ensure.NotNull(events);
			StreamMetadata = streamMetadata;
			IsCachePublic = isCachePublic;
			Error = error;
			NextEventNumber = nextEventNumber;
			LastEventNumber = lastEventNumber;
			IsEndOfStream = isEndOfStream;
			TfLastCommitPosition = tfLastCommitPosition;
		}
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class ReadStreamEventsBackward : ReadRequestMessage {
		public readonly string EventStreamId;
		public readonly long FromEventNumber;
		public readonly int MaxCount;
		public readonly bool ResolveLinkTos;
		public readonly bool RequireLeader;

		public readonly long? ValidationStreamVersion;

		public ReadStreamEventsBackward(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
			string eventStreamId, long fromEventNumber, int maxCount, bool resolveLinkTos,
			bool requireLeader, long? validationStreamVersion, ClaimsPrincipal user, DateTime? expires = null,
			CancellationToken cancellationToken = default)
			: base(internalCorrId, correlationId, envelope, user, expires, cancellationToken) {
			ArgumentOutOfRangeException.ThrowIfLessThan(fromEventNumber, -1);
			EventStreamId = Ensure.NotNullOrEmpty(eventStreamId);
			FromEventNumber = fromEventNumber;
			MaxCount = maxCount;
			ResolveLinkTos = resolveLinkTos;
			RequireLeader = requireLeader;
			ValidationStreamVersion = validationStreamVersion;
		}

		public override string ToString() =>
			$"{base.ToString()}, " +
			$"EventStreamId: {EventStreamId}, " +
			$"FromEventNumber: {FromEventNumber}, " +
			$"MaxCount: {MaxCount}, " +
			$"ResolveLinkTos: {ResolveLinkTos}, " +
			$"RequireLeader: {RequireLeader}, " +
			$"ValidationStreamVersion: {ValidationStreamVersion}";
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class ReadStreamEventsBackwardCompleted : ReadResponseMessage {
		public readonly Guid CorrelationId;
		public readonly string EventStreamId;
		public readonly long FromEventNumber;
		public readonly int MaxCount;

		public readonly ReadStreamResult Result;
		public readonly IReadOnlyList<ResolvedEvent> Events;
		public readonly StreamMetadata StreamMetadata;
		public readonly bool IsCachePublic;
		public readonly string Error;
		public readonly long NextEventNumber;
		public readonly long LastEventNumber;
		public readonly bool IsEndOfStream;
		public readonly long TfLastCommitPosition;

		public ReadStreamEventsBackwardCompleted(
			Guid correlationId,
			string eventStreamId,
			long fromEventNumber,
			int maxCount,
			ReadStreamResult result,
			IReadOnlyList<ResolvedEvent> events,
			StreamMetadata streamMetadata,
			bool isCachePublic,
			string error,
			long nextEventNumber,
			long lastEventNumber,
			bool isEndOfStream,
			long tfLastCommitPosition) {
			if (result != ReadStreamResult.Success) {
				Ensure.Equal(nextEventNumber, -1, "nextEventNumber");
				Ensure.Equal(isEndOfStream, true, "isEndOfStream");
			}

			CorrelationId = correlationId;
			EventStreamId = eventStreamId;
			FromEventNumber = fromEventNumber;
			MaxCount = maxCount;
			Result = result;
			Events = Ensure.NotNull(events);
			StreamMetadata = streamMetadata;
			IsCachePublic = isCachePublic;
			Error = error;
			NextEventNumber = nextEventNumber;
			LastEventNumber = lastEventNumber;
			IsEndOfStream = isEndOfStream;
			TfLastCommitPosition = tfLastCommitPosition;
		}
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class ReadAllEventsForward(
		Guid internalCorrId,
		Guid correlationId,
		IEnvelope envelope,
		long commitPosition,
		long preparePosition,
		int maxCount,
		bool resolveLinkTos,
		bool requireLeader,
		long? validationTfLastCommitPosition,
		ClaimsPrincipal user,
		bool replyOnExpired,
		TimeSpan? longPollTimeout = null,
		DateTime? expires = null,
		CancellationToken cancellationToken = default)
		: ReadRequestMessage(internalCorrId, correlationId, envelope, user, expires, cancellationToken) {
		public readonly long CommitPosition = commitPosition;
		public readonly long PreparePosition = preparePosition;
		public readonly int MaxCount = maxCount;
		public readonly bool ResolveLinkTos = resolveLinkTos;
		public readonly bool RequireLeader = requireLeader;

		public readonly long? ValidationTfLastCommitPosition = validationTfLastCommitPosition;
		public readonly TimeSpan? LongPollTimeout = longPollTimeout;
		public readonly bool ReplyOnExpired = replyOnExpired;

		public override string ToString() =>
			$"{base.ToString()}, " +
			$"PreparePosition: {PreparePosition}, " +
			$"CommitPosition: {CommitPosition}, " +
			$"MaxCount: {MaxCount}, " +
			$"ResolveLinkTos: {ResolveLinkTos}, " +
			$"RequireLeader: {RequireLeader}, " +
			$"ValidationTfLastCommitPosition: {ValidationTfLastCommitPosition}, " +
			$"LongPollTimeout: {LongPollTimeout}, " +
			$"ReplyOnExpired: {ReplyOnExpired}";
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class ReadAllEventsForwardCompleted : ReadResponseMessage {
		public readonly Guid CorrelationId;
		public readonly ReadAllResult Result;
		public readonly string Error;
		public readonly IReadOnlyList<ResolvedEvent> Events;
		public readonly StreamMetadata StreamMetadata;
		public readonly bool IsCachePublic;
		public readonly int MaxCount;
		public readonly TFPos CurrentPos;
		public readonly TFPos NextPos;
		public readonly TFPos PrevPos;
		public readonly long TfLastCommitPosition;

		public bool IsEndOfStream => Events == null || Events.Count < MaxCount;

		public ReadAllEventsForwardCompleted(Guid correlationId, ReadAllResult result, string error,
			IReadOnlyList<ResolvedEvent> events,
			StreamMetadata streamMetadata, bool isCachePublic, int maxCount,
			TFPos currentPos, TFPos nextPos, TFPos prevPos, long tfLastCommitPosition) {
			CorrelationId = correlationId;
			Result = result;
			Error = error;
			Events = Ensure.NotNull(events);
			StreamMetadata = streamMetadata;
			IsCachePublic = isCachePublic;
			MaxCount = maxCount;
			CurrentPos = currentPos;
			NextPos = nextPos;
			PrevPos = prevPos;
			TfLastCommitPosition = tfLastCommitPosition;
		}
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class ReadAllEventsBackward(
		Guid internalCorrId,
		Guid correlationId,
		IEnvelope envelope,
		long commitPosition,
		long preparePosition,
		int maxCount,
		bool resolveLinkTos,
		bool requireLeader,
		long? validationTfLastCommitPosition,
		ClaimsPrincipal user,
		DateTime? expires = null,
		CancellationToken cancellationToken = default)
		: ReadRequestMessage(internalCorrId, correlationId, envelope, user, expires, cancellationToken) {
		public readonly long CommitPosition = commitPosition;
		public readonly long PreparePosition = preparePosition;
		public readonly int MaxCount = maxCount;
		public readonly bool ResolveLinkTos = resolveLinkTos;
		public readonly bool RequireLeader = requireLeader;
		public readonly long? ValidationTfLastCommitPosition = validationTfLastCommitPosition;

		public override string ToString() =>
			$"{base.ToString()}, " +
			$"PreparePosition: {PreparePosition}, " +
			$"CommitPosition: {CommitPosition}, " +
			$"MaxCount: {MaxCount}, " +
			$"ResolveLinkTos: {ResolveLinkTos}, " +
			$"RequireLeader: {RequireLeader}, " +
			$"ValidationTfLastCommitPosition: {ValidationTfLastCommitPosition}";
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class ReadAllEventsBackwardCompleted : ReadResponseMessage {
		public readonly Guid CorrelationId;
		public readonly ReadAllResult Result;
		public readonly string Error;
		public readonly IReadOnlyList<ResolvedEvent> Events;
		public readonly StreamMetadata StreamMetadata;
		public readonly bool IsCachePublic;
		public readonly int MaxCount;
		public readonly TFPos CurrentPos;
		public readonly TFPos NextPos;
		public readonly TFPos PrevPos;
		public readonly long TfLastCommitPosition;

		public bool IsEndOfStream => Events == null || Events.Count < MaxCount;

		public ReadAllEventsBackwardCompleted(Guid correlationId, ReadAllResult result, string error,
			IReadOnlyList<ResolvedEvent> events,
			StreamMetadata streamMetadata, bool isCachePublic, int maxCount,
			TFPos currentPos, TFPos nextPos, TFPos prevPos, long tfLastCommitPosition) {
			CorrelationId = correlationId;
			Result = result;
			Error = error;
			Events = Ensure.NotNull(events);
			StreamMetadata = streamMetadata;
			IsCachePublic = isCachePublic;
			MaxCount = maxCount;
			CurrentPos = currentPos;
			NextPos = nextPos;
			PrevPos = prevPos;
			TfLastCommitPosition = tfLastCommitPosition;
		}
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class FilteredReadAllEventsForward(
		Guid internalCorrId,
		Guid correlationId,
		IEnvelope envelope,
		long commitPosition,
		long preparePosition,
		int maxCount,
		bool resolveLinkTos,
		bool requireLeader,
		int maxSearchWindow,
		long? validationTfLastCommitPosition,
		IEventFilter eventFilter,
		ClaimsPrincipal user,
		bool replyOnExpired,
		TimeSpan? longPollTimeout = null,
		DateTime? expires = null,
		CancellationToken cancellationToken = default)
		: ReadRequestMessage(internalCorrId, correlationId, envelope, user, expires, cancellationToken) {
		public readonly long CommitPosition = commitPosition;
		public readonly long PreparePosition = preparePosition;
		public readonly int MaxCount = maxCount;
		public readonly bool ResolveLinkTos = resolveLinkTos;
		public readonly bool RequireLeader = requireLeader;
		public readonly int MaxSearchWindow = maxSearchWindow;
		public readonly IEventFilter EventFilter = eventFilter;
		public readonly bool ReplyOnExpired = replyOnExpired;
		public readonly long? ValidationTfLastCommitPosition = validationTfLastCommitPosition;
		public readonly TimeSpan? LongPollTimeout = longPollTimeout;

		public override string ToString() =>
			$"{base.ToString()}, " +
			$"PreparePosition: {PreparePosition}, " +
			$"CommitPosition: {CommitPosition}, " +
			$"MaxCount: {MaxCount}, " +
			$"ResolveLinkTos: {ResolveLinkTos}, " +
			$"RequireLeader: {RequireLeader}, " +
			$"ValidationTfLastCommitPosition: {ValidationTfLastCommitPosition}, " +
			$"LongPollTimeout: {LongPollTimeout}, " +
			$"MaxSearchWindow: {MaxSearchWindow}, " +
			$"EventFilter: {{ {EventFilter} }}, " +
			$"ReplyOnExpired: {ReplyOnExpired}";
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class FilteredReadAllEventsForwardCompleted : ReadResponseMessage {
		public readonly Guid CorrelationId;
		public readonly FilteredReadAllResult Result;
		public readonly string Error;
		public readonly IReadOnlyList<ResolvedEvent> Events;
		public readonly StreamMetadata StreamMetadata;
		public readonly bool IsCachePublic;
		public readonly int MaxCount;
		public readonly TFPos CurrentPos;
		public readonly TFPos NextPos;
		public readonly TFPos PrevPos;
		public readonly long TfLastCommitPosition;
		public readonly bool IsEndOfStream;
		public readonly long ConsideredEventsCount;

		public FilteredReadAllEventsForwardCompleted(Guid correlationId, FilteredReadAllResult result, string error,
			IReadOnlyList<ResolvedEvent> events,
			StreamMetadata streamMetadata, bool isCachePublic, int maxCount,
			TFPos currentPos, TFPos nextPos, TFPos prevPos, long tfLastCommitPosition,
			bool isEndOfStream, long consideredEventsCount) {
			CorrelationId = correlationId;
			Result = result;
			Error = error;
			Events = Ensure.NotNull(events);
			StreamMetadata = streamMetadata;
			IsCachePublic = isCachePublic;
			MaxCount = maxCount;
			CurrentPos = currentPos;
			NextPos = nextPos;
			PrevPos = prevPos;
			TfLastCommitPosition = tfLastCommitPosition;
			IsEndOfStream = isEndOfStream;
			ConsideredEventsCount = consideredEventsCount;
		}
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class FilteredReadAllEventsBackward(
		Guid internalCorrId,
		Guid correlationId,
		IEnvelope envelope,
		long commitPosition,
		long preparePosition,
		int maxCount,
		bool resolveLinkTos,
		bool requireLeader,
		int maxSearchWindow,
		long? validationTfLastCommitPosition,
		IEventFilter eventFilter,
		ClaimsPrincipal user,
		TimeSpan? longPollTimeout = null,
		DateTime? expires = null,
		CancellationToken cancellationToken = default)
		: ReadRequestMessage(internalCorrId, correlationId, envelope, user, expires, cancellationToken) {
		public readonly long CommitPosition = commitPosition;
		public readonly long PreparePosition = preparePosition;
		public readonly int MaxCount = maxCount;
		public readonly bool ResolveLinkTos = resolveLinkTos;
		public readonly bool RequireLeader = requireLeader;
		public readonly int MaxSearchWindow = maxSearchWindow;
		public readonly IEventFilter EventFilter = eventFilter;
		public readonly long? ValidationTfLastCommitPosition = validationTfLastCommitPosition;
		public readonly TimeSpan? LongPollTimeout = longPollTimeout;

		public override string ToString() =>
			$"{base.ToString()}, " +
			$"PreparePosition: {PreparePosition}, " +
			$"CommitPosition: {CommitPosition}, " +
			$"MaxCount: {MaxCount}, " +
			$"ResolveLinkTos: {ResolveLinkTos}, " +
			$"RequireLeader: {RequireLeader}, " +
			$"MaxSearchWindow: {MaxSearchWindow}, " +
			$"EventFilter: {{ {EventFilter} }}, " +
			$"LongPollTimeout: {LongPollTimeout}, " +
			$"ValidationTfLastCommitPosition: {ValidationTfLastCommitPosition}";
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class FilteredReadAllEventsBackwardCompleted(
		Guid correlationId,
		FilteredReadAllResult result,
		string error,
		IReadOnlyList<ResolvedEvent> events,
		StreamMetadata streamMetadata,
		bool isCachePublic,
		int maxCount,
		TFPos currentPos,
		TFPos nextPos,
		TFPos prevPos,
		long tfLastCommitPosition,
		bool isEndOfStream)
		: ReadResponseMessage {
		public readonly Guid CorrelationId = correlationId;
		public readonly FilteredReadAllResult Result = result;
		public readonly string Error = error;

		public readonly IReadOnlyList<ResolvedEvent> Events = Ensure.NotNull(events);
		public readonly StreamMetadata StreamMetadata = streamMetadata;
		public readonly bool IsCachePublic = isCachePublic;
		public readonly int MaxCount = maxCount;
		public readonly TFPos CurrentPos = currentPos;
		public readonly TFPos NextPos = nextPos;
		public readonly TFPos PrevPos = prevPos;
		public readonly long TfLastCommitPosition = tfLastCommitPosition;
		public readonly bool IsEndOfStream = isEndOfStream;
	}

	//Persistent subscriptions
	[DerivedMessage(CoreMessage.Client)]
	public partial class ConnectToPersistentSubscriptionToStream(
		Guid internalCorrId,
		Guid correlationId,
		IEnvelope envelope,
		Guid connectionId,
		string connectionName,
		string groupName,
		string eventStreamId,
		int allowedInFlightMessages,
		string from,
		ClaimsPrincipal user,
		DateTime? expires = null)
		: ReadRequestMessage(internalCorrId, correlationId, envelope, user, expires) {
		public readonly Guid ConnectionId = Ensure.NotEmptyGuid(connectionId);
		public readonly string ConnectionName = connectionName;
		public readonly string GroupName = Ensure.NotNullOrEmpty(groupName);
		public readonly string EventStreamId = eventStreamId;
		public readonly int AllowedInFlightMessages = Ensure.Nonnegative(allowedInFlightMessages);
		public readonly string From = from;
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class ConnectToPersistentSubscriptionToAll(
		Guid internalCorrId,
		Guid correlationId,
		IEnvelope envelope,
		Guid connectionId,
		string connectionName,
		string groupName,
		int allowedInFlightMessages,
		string from,
		ClaimsPrincipal user,
		DateTime? expires = null)
		: ReadRequestMessage(internalCorrId, correlationId, envelope, user, expires) {
		public readonly Guid ConnectionId = Ensure.NotEmptyGuid(connectionId);
		public readonly string ConnectionName = connectionName;
		public readonly string GroupName = Ensure.NotNullOrEmpty(groupName);
		public readonly int AllowedInFlightMessages = Ensure.Nonnegative(allowedInFlightMessages);
		public readonly string From = from;
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class CreatePersistentSubscriptionToStream(
		Guid internalCorrId,
		Guid correlationId,
		IEnvelope envelope,
		string eventStreamId,
		string groupName,
		bool resolveLinkTos,
		long startFrom,
		int messageTimeoutMilliseconds,
		bool recordStatistics,
		int maxRetryCount,
		int bufferSize,
		int liveBufferSize,
		int readbatchSize,
		int checkPointAfterMilliseconds,
		int minCheckPointCount,
		int maxCheckPointCount,
		int maxSubscriberCount,
		string namedConsumerStrategy,
		ClaimsPrincipal user,
		DateTime? expires = null)
		: ReadRequestMessage(internalCorrId, correlationId, envelope, user, expires) {
		public readonly long StartFrom = startFrom;
		public readonly int MessageTimeoutMilliseconds = messageTimeoutMilliseconds;
		public readonly bool RecordStatistics = recordStatistics;

		public readonly bool ResolveLinkTos = resolveLinkTos;
		public readonly int MaxRetryCount = maxRetryCount;
		public readonly int BufferSize = bufferSize;
		public readonly int LiveBufferSize = liveBufferSize;
		public readonly int ReadBatchSize = readbatchSize;

		public readonly string GroupName = groupName;
		public readonly string EventStreamId = eventStreamId;
		public readonly int MaxSubscriberCount = maxSubscriberCount;
		public readonly string NamedConsumerStrategy = namedConsumerStrategy;
		public readonly int MaxCheckPointCount = maxCheckPointCount;
		public readonly int MinCheckPointCount = minCheckPointCount;
		public readonly int CheckPointAfterMilliseconds = checkPointAfterMilliseconds;
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class CreatePersistentSubscriptionToStreamCompleted(
		Guid correlationId,
		CreatePersistentSubscriptionToStreamCompleted.CreatePersistentSubscriptionToStreamResult result,
		string reason)
		: ReadResponseMessage {
		public readonly Guid CorrelationId = Ensure.NotEmptyGuid(correlationId);
		public readonly string Reason = reason;
		public readonly CreatePersistentSubscriptionToStreamResult Result = result;

		public enum CreatePersistentSubscriptionToStreamResult {
			Success = 0,
			AlreadyExists = 1,
			Fail = 2,
			AccessDenied = 3
		}
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class CreatePersistentSubscriptionToAll(
		Guid internalCorrId,
		Guid correlationId,
		IEnvelope envelope,
		string groupName,
		IEventFilter eventFilter,
		bool resolveLinkTos,
		TFPos startFrom,
		int messageTimeoutMilliseconds,
		bool recordStatistics,
		int maxRetryCount,
		int bufferSize,
		int liveBufferSize,
		int readbatchSize,
		int checkPointAfterMilliseconds,
		int minCheckPointCount,
		int maxCheckPointCount,
		int maxSubscriberCount,
		string namedConsumerStrategy,
		ClaimsPrincipal user,
		DateTime? expires = null)
		: ReadRequestMessage(internalCorrId, correlationId, envelope, user, expires) {
		public readonly IEventFilter EventFilter = eventFilter;

		public readonly TFPos StartFrom = startFrom;
		public readonly int MessageTimeoutMilliseconds = messageTimeoutMilliseconds;
		public readonly bool RecordStatistics = recordStatistics;

		public readonly bool ResolveLinkTos = resolveLinkTos;
		public readonly int MaxRetryCount = maxRetryCount;
		public readonly int BufferSize = bufferSize;
		public readonly int LiveBufferSize = liveBufferSize;
		public readonly int ReadBatchSize = readbatchSize;

		public readonly string GroupName = groupName;
		public readonly int MaxSubscriberCount = maxSubscriberCount;
		public readonly string NamedConsumerStrategy = namedConsumerStrategy;
		public readonly int MaxCheckPointCount = maxCheckPointCount;
		public readonly int MinCheckPointCount = minCheckPointCount;
		public readonly int CheckPointAfterMilliseconds = checkPointAfterMilliseconds;
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class CreatePersistentSubscriptionToAllCompleted(
		Guid correlationId,
		CreatePersistentSubscriptionToAllCompleted.CreatePersistentSubscriptionToAllResult result,
		string reason) : ReadResponseMessage {
		public readonly Guid CorrelationId = Ensure.NotEmptyGuid(correlationId);
		public readonly string Reason = reason;
		public readonly CreatePersistentSubscriptionToAllResult Result = result;

		public enum CreatePersistentSubscriptionToAllResult {
			Success = 0,
			AlreadyExists = 1,
			Fail = 2,
			AccessDenied = 3
		}
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class UpdatePersistentSubscriptionToStream(
		Guid internalCorrId,
		Guid correlationId,
		IEnvelope envelope,
		string eventStreamId,
		string groupName,
		bool resolveLinkTos,
		long startFrom,
		int messageTimeoutMilliseconds,
		bool recordStatistics,
		int maxRetryCount,
		int bufferSize,
		int liveBufferSize,
		int readbatchSize,
		int checkPointAfterMilliseconds,
		int minCheckPointCount,
		int maxCheckPointCount,
		int maxSubscriberCount,
		string namedConsumerStrategy,
		ClaimsPrincipal user,
		DateTime? expires = null)
		: ReadRequestMessage(internalCorrId, correlationId, envelope, user, expires) {
		public readonly long StartFrom = startFrom;
		public readonly int MessageTimeoutMilliseconds = messageTimeoutMilliseconds;
		public readonly bool RecordStatistics = recordStatistics;

		public readonly bool ResolveLinkTos = resolveLinkTos;
		public readonly int MaxRetryCount = maxRetryCount;
		public readonly int BufferSize = bufferSize;
		public readonly int LiveBufferSize = liveBufferSize;
		public readonly int ReadBatchSize = readbatchSize;

		public readonly string GroupName = groupName;
		public readonly string EventStreamId = eventStreamId;
		public readonly int MaxSubscriberCount = maxSubscriberCount;

		public readonly int MaxCheckPointCount = maxCheckPointCount;
		public readonly int MinCheckPointCount = minCheckPointCount;
		public readonly int CheckPointAfterMilliseconds = checkPointAfterMilliseconds;
		public readonly string NamedConsumerStrategy = namedConsumerStrategy;
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class UpdatePersistentSubscriptionToStreamCompleted(
		Guid correlationId,
		UpdatePersistentSubscriptionToStreamCompleted.UpdatePersistentSubscriptionToStreamResult result,
		string reason)
		: ReadResponseMessage {
		public readonly Guid CorrelationId = Ensure.NotEmptyGuid(correlationId);
		public readonly string Reason = reason;
		public readonly UpdatePersistentSubscriptionToStreamResult Result = result;

		public enum UpdatePersistentSubscriptionToStreamResult {
			Success = 0,
			DoesNotExist = 1,
			Fail = 2,
			AccessDenied = 3
		}
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class UpdatePersistentSubscriptionToAll(
		Guid internalCorrId,
		Guid correlationId,
		IEnvelope envelope,
		string groupName,
		bool resolveLinkTos,
		TFPos startFrom,
		int messageTimeoutMilliseconds,
		bool recordStatistics,
		int maxRetryCount,
		int bufferSize,
		int liveBufferSize,
		int readbatchSize,
		int checkPointAfterMilliseconds,
		int minCheckPointCount,
		int maxCheckPointCount,
		int maxSubscriberCount,
		string namedConsumerStrategy,
		ClaimsPrincipal user,
		DateTime? expires = null)
		: ReadRequestMessage(internalCorrId, correlationId, envelope, user, expires) {
		public readonly TFPos StartFrom = startFrom;
		public readonly int MessageTimeoutMilliseconds = messageTimeoutMilliseconds;
		public readonly bool RecordStatistics = recordStatistics;

		public readonly bool ResolveLinkTos = resolveLinkTos;
		public readonly int MaxRetryCount = maxRetryCount;
		public readonly int BufferSize = bufferSize;
		public readonly int LiveBufferSize = liveBufferSize;
		public readonly int ReadBatchSize = readbatchSize;

		public readonly string GroupName = groupName;
		public readonly int MaxSubscriberCount = maxSubscriberCount;

		public readonly int MaxCheckPointCount = maxCheckPointCount;
		public readonly int MinCheckPointCount = minCheckPointCount;
		public readonly int CheckPointAfterMilliseconds = checkPointAfterMilliseconds;
		public readonly string NamedConsumerStrategy = namedConsumerStrategy;
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class UpdatePersistentSubscriptionToAllCompleted(
		Guid correlationId,
		UpdatePersistentSubscriptionToAllCompleted.UpdatePersistentSubscriptionToAllResult result,
		string reason)
		: ReadResponseMessage {
		public readonly Guid CorrelationId = Ensure.NotEmptyGuid(correlationId);
		public readonly string Reason = reason;
		public readonly UpdatePersistentSubscriptionToAllResult Result = result;

		public enum UpdatePersistentSubscriptionToAllResult {
			Success = 0,
			DoesNotExist = 1,
			Fail = 2,
			AccessDenied = 3
		}
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class ReadNextNPersistentMessages(
		Guid internalCorrId,
		Guid correlationId,
		IEnvelope envelope,
		string eventStreamId,
		string groupName,
		int count,
		ClaimsPrincipal user,
		DateTime? expires = null)
		: ReadRequestMessage(internalCorrId, correlationId, envelope, user, expires) {
		public readonly string GroupName = groupName;
		public readonly string EventStreamId = eventStreamId;
		public readonly int Count = count;
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class ReadNextNPersistentMessagesCompleted(
		Guid correlationId,
		ReadNextNPersistentMessagesCompleted.ReadNextNPersistentMessagesResult result,
		string reason,
		(ResolvedEvent ResolvedEvent, int RetryCount)[] events)
		: ReadResponseMessage {
		public readonly Guid CorrelationId = Ensure.NotEmptyGuid(correlationId);
		public readonly string Reason = reason;
		public readonly ReadNextNPersistentMessagesResult Result = result;
		public readonly (ResolvedEvent ResolvedEvent, int RetryCount)[] Events = events;

		public enum ReadNextNPersistentMessagesResult {
			Success = 0,
			DoesNotExist = 1,
			Fail = 2,
			AccessDenied = 3
		}
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class DeletePersistentSubscriptionToStream(
		Guid internalCorrId,
		Guid correlationId,
		IEnvelope envelope,
		string eventStreamId,
		string groupName,
		ClaimsPrincipal user,
		DateTime? expires = null)
		: ReadRequestMessage(internalCorrId, correlationId, envelope, user, expires) {
		public readonly string GroupName = groupName;
		public readonly string EventStreamId = eventStreamId;
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class DeletePersistentSubscriptionToStreamCompleted(
		Guid correlationId,
		DeletePersistentSubscriptionToStreamCompleted.DeletePersistentSubscriptionToStreamResult result,
		string reason)
		: ReadResponseMessage {
		public readonly Guid CorrelationId = Ensure.NotEmptyGuid(correlationId);
		public readonly string Reason = reason;
		public readonly DeletePersistentSubscriptionToStreamResult Result = result;

		public enum DeletePersistentSubscriptionToStreamResult {
			Success = 0,
			DoesNotExist = 1,
			Fail = 2,
			AccessDenied = 3
		}
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class DeletePersistentSubscriptionToAll(Guid internalCorrId, Guid correlationId, IEnvelope envelope, string groupName, ClaimsPrincipal user, DateTime? expires = null)
		: ReadRequestMessage(internalCorrId, correlationId, envelope, user, expires) {
		public readonly string GroupName = groupName;
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class DeletePersistentSubscriptionToAllCompleted(
		Guid correlationId,
		DeletePersistentSubscriptionToAllCompleted.DeletePersistentSubscriptionToAllResult result,
		string reason)
		: ReadResponseMessage {
		public readonly Guid CorrelationId = Ensure.NotEmptyGuid(correlationId);
		public readonly string Reason = reason;
		public readonly DeletePersistentSubscriptionToAllResult Result = result;

		public enum DeletePersistentSubscriptionToAllResult {
			Success = 0,
			DoesNotExist = 1,
			Fail = 2,
			AccessDenied = 3
		}
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class PersistentSubscriptionAckEvents : ReadRequestMessage {
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

	[DerivedMessage(CoreMessage.Client)]
	public partial class PersistentSubscriptionNackEvents(
		Guid internalCorrId,
		Guid correlationId,
		IEnvelope envelope,
		string subscriptionId,
		string message,
		PersistentSubscriptionNackEvents.NakAction action,
		Guid[] processedEventIds,
		ClaimsPrincipal user,
		DateTime? expires = null)
		: ReadRequestMessage(internalCorrId, correlationId, envelope, user, expires) {
		public readonly string SubscriptionId = subscriptionId;
		public readonly Guid[] ProcessedEventIds = processedEventIds;
		public readonly string Message = message;
		public readonly NakAction Action = action;

		public enum NakAction {
			Unknown = 0,
			Park = 1,
			Retry = 2,
			Skip = 3,
			Stop = 4
		}
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class PersistentSubscriptionConfirmation(string subscriptionId, Guid correlationId, long lastIndexedPosition, long? lastEventNumber) : Message {
		public readonly Guid CorrelationId = correlationId;
		public readonly long LastIndexedPosition = lastIndexedPosition;
		public readonly long? LastEventNumber = lastEventNumber;
		public readonly string SubscriptionId = subscriptionId;
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class ReplayParkedMessages(
		Guid internalCorrId,
		Guid correlationId,
		IEnvelope envelope,
		string eventStreamId,
		string groupName,
		long? stopAt,
		ClaimsPrincipal user,
		DateTime? expires = null)
		: ReadRequestMessage(internalCorrId, correlationId, envelope, user, expires) {
		public readonly string EventStreamId = eventStreamId;
		public readonly string GroupName = groupName;
		public readonly long? StopAt = stopAt;
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class ReplayParkedMessage(
		Guid internalCorrId,
		Guid correlationId,
		IEnvelope envelope,
		string streamId,
		string groupName,
		ResolvedEvent @event,
		ClaimsPrincipal user,
		DateTime? expires = null)
		: ReadRequestMessage(internalCorrId, correlationId, envelope, user, expires) {
		public readonly string EventStreamId = streamId;
		public readonly string GroupName = groupName;
		public readonly ResolvedEvent Event = @event;
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class ReplayMessagesReceived(Guid correlationId, ReplayMessagesReceived.ReplayMessagesReceivedResult result, string reason) : ReadResponseMessage {
		public readonly Guid CorrelationId = Ensure.NotEmptyGuid(correlationId);
		public readonly string Reason = reason;
		public readonly ReplayMessagesReceivedResult Result = result;

		public enum ReplayMessagesReceivedResult {
			Success = 0,
			DoesNotExist = 1,
			Fail = 2,
			AccessDenied = 3
		}
	}

	//End of persistence subscriptions


	[DerivedMessage(CoreMessage.Client)]
	public partial class SubscribeToStream(
		Guid internalCorrId,
		Guid correlationId,
		IEnvelope envelope,
		Guid connectionId,
		string eventStreamId,
		bool resolveLinkTos,
		ClaimsPrincipal user,
		DateTime? expires = null)
		: ReadRequestMessage(internalCorrId, correlationId, envelope, user, expires) {
		public readonly Guid ConnectionId = Ensure.NotEmptyGuid(connectionId);
		public readonly string EventStreamId = eventStreamId; // should be empty to subscribe to all
		public readonly bool ResolveLinkTos = resolveLinkTos;
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class FilteredSubscribeToStream(
		Guid internalCorrId,
		Guid correlationId,
		IEnvelope envelope,
		Guid connectionId,
		string eventStreamId,
		bool resolveLinkTos,
		ClaimsPrincipal user,
		IEventFilter eventFilter,
		int checkpointInterval,
		int checkpointIntervalCurrent,
		DateTime? expires = null)
		: ReadRequestMessage(internalCorrId, correlationId, envelope, user, expires) {
		public readonly Guid ConnectionId = Ensure.NotEmptyGuid(connectionId);
		public readonly string EventStreamId = eventStreamId; // should be empty to subscribe to all
		public readonly bool ResolveLinkTos = resolveLinkTos;
		public readonly IEventFilter EventFilter = eventFilter;
		public readonly int CheckpointInterval = checkpointInterval;
		public readonly int CheckpointIntervalCurrent = checkpointIntervalCurrent;
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class CheckpointReached(Guid correlationId, TFPos? position) : Message {
		public readonly Guid CorrelationId = correlationId;
		public readonly TFPos? Position = position;
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class UnsubscribeFromStream(
		Guid internalCorrId,
		Guid correlationId,
		IEnvelope envelope,
		ClaimsPrincipal user,
		DateTime? expires = null)
		: ReadRequestMessage(internalCorrId, correlationId, envelope, user, expires);

	[DerivedMessage(CoreMessage.Client)]
	public partial class SubscriptionConfirmation(Guid correlationId, long lastIndexedPosition, long? lastEventNumber) : Message {
		public readonly Guid CorrelationId = correlationId;
		public readonly long LastIndexedPosition = lastIndexedPosition;
		public readonly long? LastEventNumber = lastEventNumber;
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class StreamEventAppeared(Guid correlationId, ResolvedEvent @event) : Message {
		public readonly Guid CorrelationId = correlationId;
		public readonly ResolvedEvent Event = @event;
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class PersistentSubscriptionStreamEventAppeared(Guid correlationId, ResolvedEvent @event, int retryCount) : Message {
		public readonly Guid CorrelationId = correlationId;
		public readonly ResolvedEvent Event = @event;
		public readonly int RetryCount = retryCount;
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class SubscriptionDropped(Guid correlationId, SubscriptionDropReason reason) : Message {
		public readonly Guid CorrelationId = correlationId;
		public readonly SubscriptionDropReason Reason = reason;
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class MergeIndexes(IEnvelope envelope, Guid correlationId, ClaimsPrincipal user) : Message {
		public readonly IEnvelope Envelope = Ensure.NotNull(envelope);
		public readonly Guid CorrelationId = correlationId;
		public readonly ClaimsPrincipal User = user;
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class MergeIndexesResponse(Guid correlationId, MergeIndexesResponse.MergeIndexesResult result) : Message {
		public readonly Guid CorrelationId = correlationId;
		public readonly MergeIndexesResult Result = result;

		public override string ToString() => $"Result: {Result}";

		public enum MergeIndexesResult {
			Started
		}
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class SetNodePriority(int nodePriority) : Message {
		public readonly int NodePriority = nodePriority;
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class ResignNode : Message;

	[DerivedMessage(CoreMessage.Client)]
	public partial class ScavengeDatabase : Message {
		public readonly IEnvelope Envelope;
		public readonly Guid CorrelationId;
		public readonly ClaimsPrincipal User;
		public readonly int StartFromChunk;
		public readonly int Threads;
		public readonly int? Threshold;
		public readonly int? ThrottlePercent;
		public readonly bool SyncOnly;

		public ScavengeDatabase(
			IEnvelope envelope,
			Guid correlationId,
			ClaimsPrincipal user,
			int startFromChunk,
			int threads,
			int? threshold,
			int? throttlePercent,
			bool syncOnly) {
			Envelope = Ensure.NotNull(envelope);
			CorrelationId = correlationId;
			User = user;
			StartFromChunk = startFromChunk;
			Threads = threads;
			Threshold = threshold;
			ThrottlePercent = throttlePercent;
			SyncOnly = syncOnly;
		}
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class StopDatabaseScavenge(IEnvelope envelope, Guid correlationId, ClaimsPrincipal user, string scavengeId) : Message {
		public readonly IEnvelope Envelope = Ensure.NotNull(envelope);
		public readonly Guid CorrelationId = correlationId;
		public readonly ClaimsPrincipal User = user;
		public readonly string ScavengeId = scavengeId;
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class GetCurrentDatabaseScavenge(IEnvelope envelope, Guid correlationId, ClaimsPrincipal user) : Message {
		public readonly IEnvelope Envelope = Ensure.NotNull(envelope);
		public readonly Guid CorrelationId = correlationId;
		public readonly ClaimsPrincipal User = user;
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class ScavengeDatabaseGetCurrentResponse(Guid correlationId, ScavengeDatabaseGetCurrentResponse.ScavengeResult result, string scavengeId) : Message {
		public readonly Guid CorrelationId = correlationId;
		public readonly ScavengeResult Result = result;
		public readonly string ScavengeId = scavengeId;

		public override string ToString() => $"Result: {Result}, ScavengeId: {ScavengeId}";

		public enum ScavengeResult {
			InProgress,
			Stopped
		}
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class GetLastDatabaseScavenge : Message {
		public readonly IEnvelope Envelope;
		public readonly Guid CorrelationId;
		public readonly ClaimsPrincipal User;

		public GetLastDatabaseScavenge(IEnvelope envelope, Guid correlationId, ClaimsPrincipal user) {
			Ensure.NotNull(envelope, nameof(envelope));
			Envelope = envelope;
			CorrelationId = correlationId;
			User = user;
		}
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class ScavengeDatabaseGetLastResponse : Message {
		public readonly Guid CorrelationId;
		public readonly ScavengeResult Result;
		public readonly string ScavengeId;

		public ScavengeDatabaseGetLastResponse(
			Guid correlationId,
			ScavengeResult result,
			string scavengeId) {
			CorrelationId = correlationId;
			Result = result;
			ScavengeId = scavengeId;
		}

		public override string ToString() => $"Result: {Result}, ScavengeId: {ScavengeId}";

		public enum ScavengeResult {
			Unknown,
			InProgress,
			Success,
			Stopped,
			Errored,
		}
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class ScavengeDatabaseStartedResponse(Guid correlationId, string scavengeId) : Message {
		public readonly Guid CorrelationId = correlationId;
		public readonly string ScavengeId = scavengeId;

		public override string ToString() => $"ScavengeId: {ScavengeId}";
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class ScavengeDatabaseInProgressResponse(Guid correlationId, string scavengeId, string reason) : Message {
		public readonly Guid CorrelationId = correlationId;
		public readonly string ScavengeId = scavengeId;
		public readonly string Reason = reason;

		public override string ToString() => $"ScavengeId: {ScavengeId}, Reason: {Reason}";
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class ScavengeDatabaseStoppedResponse(Guid correlationId, string scavengeId) : Message {
		public readonly Guid CorrelationId = correlationId;
		public readonly string ScavengeId = scavengeId;

		public override string ToString() => $"ScavengeId: {ScavengeId}";
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class ScavengeDatabaseNotFoundResponse(Guid correlationId, string scavengeId, string reason) : Message {
		public readonly Guid CorrelationId = correlationId;
		public readonly string ScavengeId = scavengeId;
		public readonly string Reason = reason;

		public override string ToString() => $"ScavengeId: {ScavengeId}, Reason: {Reason}";
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class ScavengeDatabaseUnauthorizedResponse(Guid correlationId, string scavengeId, string reason) : Message {
		public readonly Guid CorrelationId = correlationId;
		public readonly string ScavengeId = scavengeId;
		public readonly string Reason = reason;

		public override string ToString() => $"ScavengeId: {ScavengeId}, Reason: {Reason}";
	}

	[DerivedMessage(CoreMessage.Client)]
	public partial class IdentifyClient(Guid correlationId, int version, string connectionName) : Message {
		public readonly Guid CorrelationId = correlationId;
		public readonly int Version = version;
		public readonly string ConnectionName = connectionName;

		public override string ToString() => $"Version: {Version}, Connection Name: {ConnectionName}";
	}
}
