using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Diagnostics;
using EventStore.Core.Messaging;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Messages {
	public static partial class StorageMessage {
		public interface IPreconditionedWriteMessage {
			Guid CorrelationId { get; }
			IEnvelope Envelope { get; }
			string EventStreamId { get; }
			long ExpectedVersion { get; }
		}

		public interface IFlushableMessage {
		}

		public interface ILeaderWriteMessage {
		}

		[StatsGroup("storage")]
		public enum MessageType {
			None = 0,
			WritePrepares = 1,
			WriteDelete = 2,
			WriteCommit = 3,
			WriteTransactionStart = 4,
			WriteTransactionData = 5,
			WriteTransactionEnd = 6,
			PrepareAck = 7,
			CommitAck = 8,
			CommitIndexed = 9,
			EventCommitted = 10,
			TfEofAtNonCommitRecord = 11,
			AlreadyCommitted = 12,
			InvalidTransaction = 13,
			WrongExpectedVersion = 14,
			StreamDeleted = 15,
			RequestCompleted = 16,
			RequestManagerTimerTick = 17,
			BatchLogExpiredMessages = 18,
			EffectiveStreamAclRequest = 19,
			EffectiveStreamAclResponse = 20,
			OperationCancelledMessage = 21,
			StreamIdFromTransactionIdRequest = 22,
			StreamIdFromTransactionIdResponse = 23,
		}

		[StatsMessage(MessageType.WritePrepares)]
		public partial class WritePrepares : Message, IPreconditionedWriteMessage, IFlushableMessage, ILeaderWriteMessage {
			public Guid CorrelationId { get; private set; }
			public IEnvelope Envelope { get; private set; }

			public string EventStreamId { get; private set; }
			public long ExpectedVersion { get; private set; }
			public CancellationToken CancellationToken { get; }
			public readonly Event[] Events;

			public WritePrepares(Guid correlationId, IEnvelope envelope, string eventStreamId, long expectedVersion,
				Event[] events, CancellationToken cancellationToken) {
				CorrelationId = correlationId;
				Envelope = envelope;
				EventStreamId = eventStreamId;
				ExpectedVersion = expectedVersion;
				CancellationToken = cancellationToken;
				Events = events;
			}

			public override string ToString() {
				return string.Format(
					"WRITE_PREPARES: CorrelationId: {0}, EventStreamId: {1}, ExpectedVersion: {2}",
					CorrelationId, EventStreamId, ExpectedVersion);
			}
		}

		[StatsMessage(MessageType.WriteDelete)]
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

		[StatsMessage(MessageType.WriteCommit)]
		public partial class WriteCommit : Message, IFlushableMessage, ILeaderWriteMessage {
			public readonly Guid CorrelationId;
			public readonly IEnvelope Envelope;
			public readonly long TransactionPosition;

			public WriteCommit(Guid correlationId, IEnvelope envelope, long transactionPosition) {
				CorrelationId = correlationId;
				Envelope = envelope;
				TransactionPosition = transactionPosition;
			}
		}

		[StatsMessage(MessageType.WriteTransactionStart)]
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

		[StatsMessage(MessageType.WriteTransactionData)]
		public partial class WriteTransactionData : Message, IFlushableMessage, ILeaderWriteMessage {
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

		[StatsMessage(MessageType.WriteTransactionEnd)]
		public partial class WriteTransactionEnd : Message, IFlushableMessage, ILeaderWriteMessage {
			public readonly Guid CorrelationId;
			public readonly IEnvelope Envelope;
			public readonly long TransactionId;

			public readonly DateTime LiveUntil;

			public WriteTransactionEnd(Guid correlationId, IEnvelope envelope, long transactionId,
				DateTime liveUntil) {
				CorrelationId = correlationId;
				Envelope = envelope;
				TransactionId = transactionId;

				LiveUntil = liveUntil;
			}
		}

		[StatsMessage(MessageType.PrepareAck)]
		public partial class PrepareAck : Message {
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

		[StatsMessage(MessageType.CommitAck)]
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

		[StatsMessage(MessageType.CommitIndexed)]
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

		[StatsMessage(MessageType.EventCommitted)]
		public partial class EventCommitted : Message {
			public readonly long CommitPosition;
			public readonly EventRecord Event;
			public readonly bool TfEof;

			public EventCommitted(long commitPosition, EventRecord @event, bool isTfEof) {
				CommitPosition = commitPosition;
				Event = @event;
				TfEof = isTfEof;
			}
		}

		[StatsMessage(MessageType.TfEofAtNonCommitRecord)]
		public partial class TfEofAtNonCommitRecord : Message {
			public TfEofAtNonCommitRecord() {
			}
		}

		[StatsMessage(MessageType.AlreadyCommitted)]
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
				Ensure.Nonnegative(firstEventNumber, "FirstEventNumber");

				CorrelationId = correlationId;
				EventStreamId = eventStreamId;
				FirstEventNumber = firstEventNumber;
				LastEventNumber = lastEventNumber;
				LogPosition = logPosition;
			}

			public override string ToString() {
				return string.Format(
					"EventStreamId: {0}, CorrelationId: {1}, FirstEventNumber: {2}, LastEventNumber: {3}",
					EventStreamId, CorrelationId, FirstEventNumber, LastEventNumber);
			}
		}

		[StatsMessage(MessageType.InvalidTransaction)]
		public partial class InvalidTransaction : Message {
			public readonly Guid CorrelationId;

			public InvalidTransaction(Guid correlationId) {
				CorrelationId = correlationId;
			}
		}

		[StatsMessage(MessageType.WrongExpectedVersion)]
		public partial class WrongExpectedVersion : Message {
			public readonly Guid CorrelationId;
			public readonly long CurrentVersion;

			public WrongExpectedVersion(Guid correlationId, long currentVersion) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				CorrelationId = correlationId;
				CurrentVersion = currentVersion;
			}
		}

		[StatsMessage(MessageType.StreamDeleted)]
		public partial class StreamDeleted : Message {
			public readonly Guid CorrelationId;

			public StreamDeleted(Guid correlationId) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				CorrelationId = correlationId;
			}
		}

		[StatsMessage(MessageType.RequestCompleted)]
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

		[StatsMessage(MessageType.RequestManagerTimerTick)]
		public partial class RequestManagerTimerTick : Message {
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

		[StatsMessage(MessageType.BatchLogExpiredMessages)]
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

		[StatsMessage(MessageType.EffectiveStreamAclRequest)]
		public partial class EffectiveStreamAclRequest : Message {
			public readonly string StreamId;
			public readonly IEnvelope Envelope;
			public readonly CancellationToken CancellationToken;

			public EffectiveStreamAclRequest(string streamId, IEnvelope envelope, CancellationToken cancellationToken) {
				StreamId = streamId;
				Envelope = envelope;
				CancellationToken = cancellationToken;
			}
		}

		[StatsMessage(MessageType.EffectiveStreamAclResponse)]
		public partial class EffectiveStreamAclResponse : Message {
			public readonly EffectiveAcl Acl;

			public EffectiveStreamAclResponse(EffectiveAcl acl) {
				Acl = acl;
			}
		}

		public class EffectiveAcl {
			public readonly StreamAcl Stream;
			public readonly StreamAcl System;
			public readonly StreamAcl Default;

			public EffectiveAcl(StreamAcl stream, StreamAcl system, StreamAcl @default) {
				Stream = stream;
				System = system;
				Default = @default;
			}

			public static Task<EffectiveAcl> LoadAsync(IPublisher publisher, string streamId, CancellationToken cancellationToken) {
				var envelope = new RequestEffectiveAclEnvelope();
				publisher.Publish(new EffectiveStreamAclRequest(streamId, envelope, cancellationToken));
				return envelope.Task;
			}

			class RequestEffectiveAclEnvelope : IEnvelope {
				private readonly TaskCompletionSource<EffectiveAcl> _tcs;

				public RequestEffectiveAclEnvelope() {
					_tcs = new TaskCompletionSource<EffectiveAcl>(TaskCreationOptions.RunContinuationsAsynchronously);
				}
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

		[StatsMessage(MessageType.OperationCancelledMessage)]
		public partial class OperationCancelledMessage : Message {
			public CancellationToken CancellationToken { get; }

			public OperationCancelledMessage(CancellationToken cancellationToken) {
				CancellationToken = cancellationToken;
			}
		}

		[StatsMessage(MessageType.StreamIdFromTransactionIdRequest)]
		public partial class StreamIdFromTransactionIdRequest : Message {
			public readonly long TransactionId;
			public readonly IEnvelope Envelope;
			public readonly CancellationToken CancellationToken;

			public StreamIdFromTransactionIdRequest(in long transactionId, IEnvelope envelope, CancellationToken cancellationToken) {
				CancellationToken = cancellationToken;
				TransactionId = transactionId;
				Envelope = envelope;
			}
		}

		[StatsMessage(MessageType.StreamIdFromTransactionIdResponse)]
		public partial class StreamIdFromTransactionIdResponse : Message {
			public readonly string StreamId;

			public StreamIdFromTransactionIdResponse(string streamId){
				StreamId = streamId;
			}
		}
	}
}
