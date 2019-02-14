using System;
using System.Security.Principal;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Messages {
	public static class StorageMessage {
		public interface IPreconditionedWriteMessage {
			Guid CorrelationId { get; }
			IEnvelope Envelope { get; }
			string EventStreamId { get; }
			long ExpectedVersion { get; }
		}

		public interface IFlushableMessage {
		}

		public interface IMasterWriteMessage {
		}

		public class WritePrepares : Message, IPreconditionedWriteMessage, IFlushableMessage, IMasterWriteMessage {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public Guid CorrelationId { get; private set; }
			public IEnvelope Envelope { get; private set; }

			public string EventStreamId { get; private set; }
			public long ExpectedVersion { get; private set; }
			public readonly Event[] Events;

			public readonly DateTime LiveUntil;

			public WritePrepares(Guid correlationId, IEnvelope envelope, string eventStreamId, long expectedVersion,
				Event[] events, DateTime liveUntil) {
				CorrelationId = correlationId;
				Envelope = envelope;
				EventStreamId = eventStreamId;
				ExpectedVersion = expectedVersion;
				Events = events;

				LiveUntil = liveUntil;
			}

			public override string ToString() {
				return string.Format(
					"WRITE_PREPARES: CorrelationId: {0}, EventStreamId: {1}, ExpectedVersion: {2}, LiveUntil: {3}",
					CorrelationId, EventStreamId, ExpectedVersion, LiveUntil);
			}
		}

		public class WriteDelete : Message, IPreconditionedWriteMessage, IFlushableMessage, IMasterWriteMessage {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public Guid CorrelationId { get; private set; }
			public IEnvelope Envelope { get; private set; }
			public string EventStreamId { get; private set; }
			public long ExpectedVersion { get; private set; }
			public readonly bool HardDelete;

			public readonly DateTime LiveUntil;

			public WriteDelete(Guid correlationId, IEnvelope envelope, string eventStreamId, long expectedVersion,
				bool hardDelete, DateTime liveUntil) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				Ensure.NotNull(envelope, "envelope");
				Ensure.NotNull(eventStreamId, "eventStreamId");

				CorrelationId = correlationId;
				Envelope = envelope;
				EventStreamId = eventStreamId;
				ExpectedVersion = expectedVersion;
				HardDelete = hardDelete;

				LiveUntil = liveUntil;
			}
		}

		public class WriteCommit : Message, IFlushableMessage, IMasterWriteMessage {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly IEnvelope Envelope;
			public readonly long TransactionPosition;

			public WriteCommit(Guid correlationId, IEnvelope envelope, long transactionPosition) {
				CorrelationId = correlationId;
				Envelope = envelope;
				TransactionPosition = transactionPosition;
			}
		}

		public class WriteTransactionStart : Message, IPreconditionedWriteMessage, IFlushableMessage,
			IMasterWriteMessage {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

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

		public class WriteTransactionData : Message, IFlushableMessage, IMasterWriteMessage {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly IEnvelope Envelope;
			public readonly long TransactionId;
			public readonly Event[] Events;

			public WriteTransactionData(Guid correlationId, IEnvelope envelope, long transactionId, Event[] events) {
				CorrelationId = correlationId;
				Envelope = envelope;
				TransactionId = transactionId;
				Events = events;
			}
		}

		public class WriteTransactionPrepare : Message, IFlushableMessage, IMasterWriteMessage {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly IEnvelope Envelope;
			public readonly long TransactionId;

			public readonly DateTime LiveUntil;

			public WriteTransactionPrepare(Guid correlationId, IEnvelope envelope, long transactionId,
				DateTime liveUntil) {
				CorrelationId = correlationId;
				Envelope = envelope;
				TransactionId = transactionId;

				LiveUntil = liveUntil;
			}
		}

		public class PrepareAck : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly long LogPosition;
			public readonly PrepareFlags Flags;

			public PrepareAck(Guid correlationId, long logPosition, PrepareFlags flags) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				Ensure.Nonnegative(logPosition, "logPosition");

				CorrelationId = correlationId;
				LogPosition = logPosition;
				Flags = flags;
			}
		}

		public class CommitAck : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly long LogPosition;
			public readonly long TransactionPosition;
			public readonly long FirstEventNumber;
			public readonly long LastEventNumber;
			public readonly bool IsSelf;

			public CommitAck(Guid correlationId, long logPosition, long transactionPosition, long firstEventNumber,
				long lastEventNumber, bool isSelf = false) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				Ensure.Nonnegative(logPosition, "logPosition");
				Ensure.Nonnegative(transactionPosition, "transactionPosition");
				if (firstEventNumber < -1)
					throw new ArgumentOutOfRangeException("firstEventNumber",
						string.Format("FirstEventNumber: {0}", firstEventNumber));
				if (lastEventNumber - firstEventNumber + 1 < 0)
					throw new ArgumentOutOfRangeException("lastEventNumber",
						string.Format("LastEventNumber {0}, FirstEventNumber {1}.", lastEventNumber, firstEventNumber));

				CorrelationId = correlationId;
				LogPosition = logPosition;
				TransactionPosition = transactionPosition;
				FirstEventNumber = firstEventNumber;
				LastEventNumber = lastEventNumber;
				IsSelf = isSelf;
			}
		}

		public class CommitReplicated : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly long LogPosition;
			public readonly long TransactionPosition;
			public readonly long FirstEventNumber;
			public readonly long LastEventNumber;

			public CommitReplicated(Guid correlationId, long logPosition, long transactionPosition,
				long firstEventNumber, long lastEventNumber) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				Ensure.Nonnegative(logPosition, "logPosition");
				Ensure.Nonnegative(transactionPosition, "transactionPosition");
				if (firstEventNumber < -1)
					throw new ArgumentOutOfRangeException("firstEventNumber",
						string.Format("FirstEventNumber: {0}", firstEventNumber));
				if (lastEventNumber - firstEventNumber + 1 < 0)
					throw new ArgumentOutOfRangeException("lastEventNumber",
						string.Format("LastEventNumber {0}, FirstEventNumber {1}.", lastEventNumber, firstEventNumber));
				CorrelationId = correlationId;
				LogPosition = logPosition;
				TransactionPosition = transactionPosition;
				FirstEventNumber = firstEventNumber;
				LastEventNumber = lastEventNumber;
			}
		}

		public class EventCommitted : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly long CommitPosition;
			public readonly EventRecord Event;
			public readonly bool TfEof;

			public EventCommitted(long commitPosition, EventRecord @event, bool isTfEof) {
				CommitPosition = commitPosition;
				Event = @event;
				TfEof = isTfEof;
			}
		}

		public class TfEofAtNonCommitRecord : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public TfEofAtNonCommitRecord() {
			}
		}

		public class AlreadyCommitted : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;

			public readonly string EventStreamId;
			public readonly long FirstEventNumber;
			public readonly long LastEventNumber;

			public AlreadyCommitted(Guid correlationId, string eventStreamId, long firstEventNumber,
				long lastEventNumber) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
				Ensure.Nonnegative(firstEventNumber, "FirstEventNumber");


				CorrelationId = correlationId;
				EventStreamId = eventStreamId;
				FirstEventNumber = firstEventNumber;
				LastEventNumber = lastEventNumber;
			}

			public override string ToString() {
				return string.Format(
					"EventStreamId: {0}, CorrelationId: {1}, FirstEventNumber: {2}, LastEventNumber: {3}",
					EventStreamId, CorrelationId, FirstEventNumber, LastEventNumber);
			}
		}

		public class InvalidTransaction : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;

			public InvalidTransaction(Guid correlationId) {
				CorrelationId = correlationId;
			}
		}

		public class WrongExpectedVersion : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly long CurrentVersion;

			public WrongExpectedVersion(Guid correlationId, long currentVersion) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				CorrelationId = correlationId;
				CurrentVersion = currentVersion;
			}
		}

		public class StreamDeleted : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;

			public StreamDeleted(Guid correlationId) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				CorrelationId = correlationId;
			}
		}

		public class RequestCompleted : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

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

		public class RequestManagerTimerTick : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public DateTime UtcNow {
				get { return _now ?? DateTime.UtcNow; }
			}

			private readonly DateTime? _now;

			public RequestManagerTimerTick() {
			}

			public RequestManagerTimerTick(DateTime now) {
				_now = now;
			}
		}

		public class CheckStreamAccess : ClientMessage.ReadRequestMessage, IQueueAffineMessage {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			private readonly int _affinity;

			public int QueueId {
				get { return _affinity; }
			}

			public readonly string EventStreamId;
			public readonly long? TransactionId;
			public readonly StreamAccessType AccessType;

			public CheckStreamAccess(IEnvelope envelope, Guid correlationId, string eventStreamId, long? transactionId,
				StreamAccessType accessType, IPrincipal user, bool singleAffinity = false)
				: base(correlationId, correlationId, envelope, user) {
				if (eventStreamId == null && transactionId == null)
					throw new ArgumentException("Neither eventStreamId nor transactionId is specified.");
				EventStreamId = eventStreamId;
				TransactionId = transactionId;
				var hash = String.IsNullOrEmpty(EventStreamId)
					? TransactionId.GetHashCode()
					: EventStreamId.GetHashCode();
				_affinity = singleAffinity ? 1 : hash;
				AccessType = accessType;
			}
		}

		public class CheckStreamAccessCompleted : ClientMessage.ReadResponseMessage {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly string EventStreamId;
			public readonly long? TransactionId;
			public readonly StreamAccessType AccessType;
			public readonly StreamAccess AccessResult;

			public CheckStreamAccessCompleted(Guid correlationId, string eventStreamId, long? transactionId,
				StreamAccessType accessType, StreamAccess accessResult) {
				CorrelationId = correlationId;
				EventStreamId = eventStreamId;
				TransactionId = transactionId;
				AccessType = accessType;
				AccessResult = accessResult;
			}
		}

		public class BatchLogExpiredMessages : Message, IQueueAffineMessage {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public int QueueId { get; }

			public BatchLogExpiredMessages(Guid correlationId, int queueId) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				Ensure.Nonnegative(queueId, "queueId");
				CorrelationId = correlationId;
				QueueId = queueId;
			}
		}
	}
}
