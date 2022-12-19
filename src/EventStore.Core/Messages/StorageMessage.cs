using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
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

		[DerivedMessage]
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

		[DerivedMessage]
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

		[DerivedMessage]
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

		[DerivedMessage]
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

		[DerivedMessage]
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

		[DerivedMessage]
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

		[DerivedMessage]
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

		[DerivedMessage]
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

		[DerivedMessage]
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

		[DerivedMessage]
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

		[DerivedMessage]
		public partial class TfEofAtNonCommitRecord : Message {
			public TfEofAtNonCommitRecord() {
			}
		}

		[DerivedMessage]
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

		[DerivedMessage]
		public partial class InvalidTransaction : Message {
			public readonly Guid CorrelationId;

			public InvalidTransaction(Guid correlationId) {
				CorrelationId = correlationId;
			}
		}

		[DerivedMessage]
		public partial class WrongExpectedVersion : Message {
			public readonly Guid CorrelationId;
			public readonly long CurrentVersion;

			public WrongExpectedVersion(Guid correlationId, long currentVersion) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				CorrelationId = correlationId;
				CurrentVersion = currentVersion;
			}
		}

		[DerivedMessage]
		public partial class StreamDeleted : Message {
			public readonly Guid CorrelationId;

			public StreamDeleted(Guid correlationId) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				CorrelationId = correlationId;
			}
		}

		[DerivedMessage]
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

		[DerivedMessage]
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

		[DerivedMessage]
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

		[DerivedMessage]
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

		[DerivedMessage]
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

		[DerivedMessage]
		public partial class OperationCancelledMessage : Message {
			public CancellationToken CancellationToken { get; }

			public OperationCancelledMessage(CancellationToken cancellationToken) {
				CancellationToken = cancellationToken;
			}
		}

		[DerivedMessage]
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

		[DerivedMessage]
		public partial class StreamIdFromTransactionIdResponse : Message {
			public readonly string StreamId;

			public StreamIdFromTransactionIdResponse(string streamId){
				StreamId = streamId;
			}
		}
	}
}
