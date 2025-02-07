// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messaging;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Messages;

public static partial class StorageMessage {
	public interface IPreconditionedWriteMessage {
		Guid CorrelationId { get; }
		IEnvelope Envelope { get; }
		string EventStreamId { get; }
		long ExpectedVersion { get; }
	}

	public interface IFlushableMessage;

	public interface ILeaderWriteMessage;

	[DerivedMessage(CoreMessage.Storage)]
	public partial class WritePrepares(
		Guid correlationId,
		IEnvelope envelope,
		string eventStreamId,
		long expectedVersion,
		Event[] events,
		CancellationToken cancellationToken)
		: Message, IPreconditionedWriteMessage, IFlushableMessage, ILeaderWriteMessage {
		public Guid CorrelationId { get; private set; } = correlationId;
		public IEnvelope Envelope { get; private set; } = envelope;

		public string EventStreamId { get; private set; } = eventStreamId;
		public long ExpectedVersion { get; private set; } = expectedVersion;
		public CancellationToken CancellationToken { get; } = cancellationToken;
		public readonly Event[] Events = events;

		public override string ToString() =>
			$"{GetType().Name} " +
			$"CorrelationId: {CorrelationId}, " +
			$"EventStreamId: {EventStreamId}, " +
			$"ExpectedVersion: {ExpectedVersion}, " +
			$"Envelope: {{ {Envelope} }}, " +
			$"NumEvents: {Events?.Length}, " +
			$"DataBytes: {Events?.Sum(static e => e.Data.Length)}, " +
			$"MetadataBytes: {Events?.Sum(static e => e.Metadata.Length)}";
	}

	[DerivedMessage(CoreMessage.Storage)]
	public partial class WriteDelete : Message, IPreconditionedWriteMessage, IFlushableMessage, ILeaderWriteMessage {
		public Guid CorrelationId { get; private set; }
		public IEnvelope Envelope { get; private set; }
		public string EventStreamId { get; private set; }
		public long ExpectedVersion { get; private set; }
		public CancellationToken CancellationToken { get; }
		public readonly bool HardDelete;

		public WriteDelete(Guid correlationId, IEnvelope envelope, string eventStreamId, long expectedVersion,
			bool hardDelete, CancellationToken cancellationToken = default) {
			Ensure.NotEmptyGuid(correlationId, "correlationId");
			Ensure.NotNull(envelope, "envelope");
			Ensure.NotNull(eventStreamId, "eventStreamId");

			CorrelationId = correlationId;
			Envelope = envelope;
			EventStreamId = eventStreamId;
			ExpectedVersion = expectedVersion;
			CancellationToken = cancellationToken;
			HardDelete = hardDelete;
		}
	}

	[DerivedMessage(CoreMessage.Storage)]
	public partial class WriteCommit(Guid correlationId, IEnvelope envelope, long transactionPosition) : Message, IFlushableMessage, ILeaderWriteMessage {
		public readonly Guid CorrelationId = correlationId;
		public readonly IEnvelope Envelope = envelope;
		public readonly long TransactionPosition = transactionPosition;
	}

	[DerivedMessage(CoreMessage.Storage)]
	public partial class WriteTransactionStart : Message, IPreconditionedWriteMessage, IFlushableMessage,
		ILeaderWriteMessage {

		public Guid CorrelationId { get; private set; }
		public IEnvelope Envelope { get; private set; }
		public string EventStreamId { get; private set; }
		public long ExpectedVersion { get; private set; }

		public readonly DateTime LiveUntil;

		public WriteTransactionStart(Guid correlationId, IEnvelope envelope, string eventStreamId,
			long expectedVersion, DateTime liveUntil) {
			Ensure.NotEmptyGuid(correlationId, "correlationId");
			Ensure.NotNull(envelope, "envelope");
			Ensure.NotNull(eventStreamId, "eventStreamId");

			CorrelationId = correlationId;
			Envelope = envelope;
			EventStreamId = eventStreamId;
			ExpectedVersion = expectedVersion;

			LiveUntil = liveUntil;
		}
	}

	[DerivedMessage(CoreMessage.Storage)]
	public partial class WriteTransactionData(Guid correlationId, IEnvelope envelope, long transactionId, Event[] events)
		: Message, IFlushableMessage, ILeaderWriteMessage {
		public readonly Guid CorrelationId = correlationId;
		public readonly IEnvelope Envelope = envelope;
		public readonly long TransactionId = transactionId;
		public readonly Event[] Events = events;
	}

	[DerivedMessage(CoreMessage.Storage)]
	public partial class WriteTransactionEnd(
		Guid correlationId,
		IEnvelope envelope,
		long transactionId,
		DateTime liveUntil)
		: Message, IFlushableMessage, ILeaderWriteMessage {
		public readonly Guid CorrelationId = correlationId;
		public readonly IEnvelope Envelope = envelope;
		public readonly long TransactionId = transactionId;

		public readonly DateTime LiveUntil = liveUntil;
	}

	[DerivedMessage(CoreMessage.Storage)]
	public partial class PrepareAck : Message {
		public readonly Guid CorrelationId;
		public readonly long LogPosition;
		public readonly PrepareFlags Flags;

		public PrepareAck(Guid correlationId, long logPosition, PrepareFlags flags) {
			Debug.Assert(correlationId != Guid.Empty);
			Debug.Assert(logPosition >= 0);

			CorrelationId = correlationId;
			LogPosition = logPosition;
			Flags = flags;
		}
	}

	[DerivedMessage(CoreMessage.Storage)]
	public partial class CommitAck : Message {
		public readonly Guid CorrelationId;
		public readonly long LogPosition;
		public readonly long TransactionPosition;
		public readonly long FirstEventNumber;
		public readonly long LastEventNumber;

		public CommitAck(Guid correlationId, long logPosition, long transactionPosition, long firstEventNumber,
			long lastEventNumber) {
			Ensure.NotEmptyGuid(correlationId, "correlationId");
			Ensure.Nonnegative(logPosition, "logPosition");
			Ensure.Nonnegative(transactionPosition, "transactionPosition");
			if (firstEventNumber < -1)
				throw new ArgumentOutOfRangeException(nameof(firstEventNumber),
					$"FirstEventNumber: {firstEventNumber}");
			if (lastEventNumber - firstEventNumber + 1 < 0)
				throw new ArgumentOutOfRangeException(nameof(lastEventNumber),
					$"LastEventNumber {lastEventNumber}, FirstEventNumber {firstEventNumber}.");

			CorrelationId = correlationId;
			LogPosition = logPosition;
			TransactionPosition = transactionPosition;
			FirstEventNumber = firstEventNumber;
			LastEventNumber = lastEventNumber;
		}
	}

	[DerivedMessage(CoreMessage.Storage)]
	public partial class CommitIndexed : Message {
		public readonly Guid CorrelationId;
		public readonly long LogPosition;
		public readonly long TransactionPosition;
		public readonly long FirstEventNumber;
		public readonly long LastEventNumber;

		public CommitIndexed(Guid correlationId, long logPosition, long transactionPosition,
			long firstEventNumber, long lastEventNumber) {
			Ensure.NotEmptyGuid(correlationId, "correlationId");
			Ensure.Nonnegative(logPosition, "logPosition");
			Ensure.Nonnegative(transactionPosition, "transactionPosition");
			if (firstEventNumber < -1)
				throw new ArgumentOutOfRangeException(nameof(firstEventNumber), $"FirstEventNumber: {firstEventNumber}");
			if (lastEventNumber - firstEventNumber + 1 < 0)
				throw new ArgumentOutOfRangeException(nameof(lastEventNumber), $"LastEventNumber {lastEventNumber}, FirstEventNumber {firstEventNumber}.");
			CorrelationId = correlationId;
			LogPosition = logPosition;
			TransactionPosition = transactionPosition;
			FirstEventNumber = firstEventNumber;
			LastEventNumber = lastEventNumber;
		}
	}

	[DerivedMessage(CoreMessage.Storage)]
	public partial class EventCommitted(long commitPosition, EventRecord @event, bool isTfEof) : Message {
		public readonly long CommitPosition = commitPosition;
		public readonly EventRecord Event = @event;
		public readonly bool TfEof = isTfEof;
	}

	[DerivedMessage(CoreMessage.Storage)]
	public partial class InMemoryEventCommitted(long commitPosition, EventRecord @event) : Message {
		public readonly long CommitPosition = commitPosition;
		public readonly EventRecord Event = @event;
	}

	[DerivedMessage(CoreMessage.Storage)]
	public partial class TfEofAtNonCommitRecord : Message;

	[DerivedMessage(CoreMessage.Storage)]
	public partial class AlreadyCommitted : Message {
		public readonly Guid CorrelationId;

		public readonly string EventStreamId;
		public readonly long FirstEventNumber;
		public readonly long LastEventNumber;
		public readonly long LogPosition;

		public AlreadyCommitted(Guid correlationId, string eventStreamId, long firstEventNumber,
			long lastEventNumber, long logPosition) {
			Ensure.NotEmptyGuid(correlationId, "correlationId");
			Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
			Ensure.Nonnegative(firstEventNumber, "firstEventNumber");

			CorrelationId = correlationId;
			EventStreamId = eventStreamId;
			FirstEventNumber = firstEventNumber;
			LastEventNumber = lastEventNumber;
			LogPosition = logPosition;
		}

		public override string ToString() {
			return $"EventStreamId: {EventStreamId}, CorrelationId: {CorrelationId}, FirstEventNumber: {FirstEventNumber}, LastEventNumber: {LastEventNumber}";
		}
	}

	[DerivedMessage(CoreMessage.Storage)]
	public partial class InvalidTransaction(Guid correlationId) : Message {
		public readonly Guid CorrelationId = correlationId;
	}

	[DerivedMessage(CoreMessage.Storage)]
	public partial class WrongExpectedVersion : Message {
		public readonly Guid CorrelationId;
		public readonly long CurrentVersion;

		public WrongExpectedVersion(Guid correlationId, long currentVersion) {
			Ensure.NotEmptyGuid(correlationId, "correlationId");
			CorrelationId = correlationId;
			CurrentVersion = currentVersion;
		}
	}

	[DerivedMessage(CoreMessage.Storage)]
	public partial class StreamDeleted : Message {
		public readonly Guid CorrelationId;

		public StreamDeleted(Guid correlationId) {
			Ensure.NotEmptyGuid(correlationId, "correlationId");
			CorrelationId = correlationId;
		}
	}

	[DerivedMessage(CoreMessage.Storage)]
	public partial class RequestCompleted : Message {
		public readonly Guid CorrelationId;
		public readonly bool Success;
		public readonly long CurrentVersion;

		public RequestCompleted(Guid correlationId, bool success, long currentVersion = -1) {
			Ensure.NotEmptyGuid(correlationId, "correlationId");
			CorrelationId = correlationId;
			Success = success;
			CurrentVersion = currentVersion;
		}
	}

	[DerivedMessage(CoreMessage.Storage)]
	public partial class RequestManagerTimerTick : Message {
		public DateTime UtcNow => _now ?? DateTime.UtcNow;

		private readonly DateTime? _now;

		public RequestManagerTimerTick() {
		}

		public RequestManagerTimerTick(DateTime now) {
			_now = now;
		}
	}

	[DerivedMessage(CoreMessage.Storage)]
	public partial class BatchLogExpiredMessages : Message, IQueueAffineMessage {
		public readonly Guid CorrelationId;
		public int QueueId { get; }

		public BatchLogExpiredMessages(Guid correlationId, int queueId) {
			Ensure.NotEmptyGuid(correlationId, "correlationId");
			Ensure.Nonnegative(queueId, "queueId");
			CorrelationId = correlationId;
			QueueId = queueId;
		}
	}

	[DerivedMessage(CoreMessage.Storage)]
	public partial class EffectiveStreamAclRequest(string streamId, IEnvelope envelope, CancellationToken cancellationToken) : Message {
		public readonly string StreamId = streamId;
		public readonly IEnvelope Envelope = envelope;
		public readonly CancellationToken CancellationToken = cancellationToken;
	}

	[DerivedMessage(CoreMessage.Storage)]
	public partial class EffectiveStreamAclResponse(EffectiveAcl acl) : Message {
		public readonly EffectiveAcl Acl = acl;
	}

	public class EffectiveAcl(StreamAcl stream, StreamAcl system, StreamAcl @default) {
		public readonly StreamAcl Stream = stream;
		public readonly StreamAcl System = system;
		public readonly StreamAcl Default = @default;

		public static Task<EffectiveAcl> LoadAsync(IPublisher publisher, string streamId, CancellationToken cancellationToken) {
			var envelope = new RequestEffectiveAclEnvelope();
			publisher.Publish(new EffectiveStreamAclRequest(streamId, envelope, cancellationToken));
			return envelope.Task;
		}

		class RequestEffectiveAclEnvelope : IEnvelope {
			private readonly TaskCompletionSource<EffectiveAcl> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

			public void ReplyWith<T>(T message) where T : Message {
				if (message == null) throw new ArgumentNullException(nameof(message));
				if (message is EffectiveStreamAclResponse response) {
					_tcs.TrySetResult(response.Acl);
					return;
				} else {
					if (message is OperationCancelledMessage cancelled) {
						_tcs.TrySetCanceled(cancelled.CancellationToken);
					}
				}
				throw new ArgumentException($"Unexpected message type {typeof(T)}");
			}

			public Task<EffectiveAcl> Task => _tcs.Task;
		}
	}

	[DerivedMessage(CoreMessage.Storage)]
	public partial class OperationCancelledMessage(CancellationToken cancellationToken) : Message {
		public CancellationToken CancellationToken { get; } = cancellationToken;
	}

	[DerivedMessage(CoreMessage.Storage)]
	public partial class StreamIdFromTransactionIdRequest(in long transactionId, IEnvelope envelope, CancellationToken cancellationToken) : Message {
		public readonly long TransactionId = transactionId;
		public readonly IEnvelope Envelope = envelope;
		public readonly CancellationToken CancellationToken = cancellationToken;
	}

	[DerivedMessage(CoreMessage.Storage)]
	public partial class StreamIdFromTransactionIdResponse(string streamId) : Message {
		public readonly string StreamId = streamId;
	}
}
