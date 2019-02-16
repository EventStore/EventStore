using System;
using System.Threading;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Messages {
	public static class EventReaderSubscriptionMessage {
		/// <summary>
		/// A CheckpointSuggested message is sent to core projection 
		/// to allow bookmarking a position that can be used to 
		/// restore the projection processing (typically
		/// an event at this position does not satisfy projection filter)
		/// </summary>
		public class CheckpointSuggested : EventReaderSubscriptionMessageBase {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public CheckpointSuggested(
				Guid subscriptionId, CheckpointTag checkpointTag, float progress,
				long subscriptionMessageSequenceNumber, object source = null)
				: base(subscriptionId, checkpointTag, progress, subscriptionMessageSequenceNumber, source) {
			}
		}

		public class ProgressChanged : EventReaderSubscriptionMessageBase {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public ProgressChanged(
				Guid subscriptionId, CheckpointTag checkpointTag, float progress,
				long subscriptionMessageSequenceNumber, object source = null)
				: base(subscriptionId, checkpointTag, progress, subscriptionMessageSequenceNumber, source) {
			}
		}

		public class SubscriptionStarted : EventReaderSubscriptionMessageBase {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			private readonly long _startingLastCommitPosition;

			public long StartingLastCommitPosition {
				get { return _startingLastCommitPosition; }
			}

			public SubscriptionStarted(
				Guid subscriptionId, CheckpointTag checkpointTag, long startingLastCommitPosition,
				long subscriptionMessageSequenceNumber, object source = null)
				: base(subscriptionId, checkpointTag, 0f, subscriptionMessageSequenceNumber, source) {
				_startingLastCommitPosition = startingLastCommitPosition;
			}
		}

		public sealed class NotAuthorized : EventReaderSubscriptionMessageBase {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public NotAuthorized(
				Guid subscriptionId, CheckpointTag checkpointTag, float progress,
				long subscriptionMessageSequenceNumber,
				object source = null)
				: base(subscriptionId, checkpointTag, progress, subscriptionMessageSequenceNumber, source) {
			}
		}

		public sealed class Failed : EventReaderSubscriptionMessageBase {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			private readonly string _reason;

			public string Reason {
				get { return _reason; }
			}

			public Failed(Guid subscriptionId, string reason)
				: base(subscriptionId, CheckpointTag.Empty, 100.0f, -1, null) {
				_reason = reason;
			}
		}

		public class EofReached : EventReaderSubscriptionMessageBase {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public EofReached(
				Guid subscriptionId, CheckpointTag checkpointTag,
				long subscriptionMessageSequenceNumber, object source = null)
				: base(subscriptionId, checkpointTag, 100.0f, subscriptionMessageSequenceNumber, source) {
			}
		}

		public class PartitionEofReached : EventReaderSubscriptionMessageBase {
			private readonly string _partition;
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public string Partition {
				get { return _partition; }
			}

			public PartitionEofReached(
				Guid subscriptionId, CheckpointTag checkpointTag, string partition,
				long subscriptionMessageSequenceNumber, object source = null)
				: base(subscriptionId, checkpointTag, 100.0f, subscriptionMessageSequenceNumber, source) {
				_partition = partition;
			}
		}

		public class PartitionMeasured : EventReaderSubscriptionMessageBase {
			private readonly string _partition;
			private readonly long _size;
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public string Partition {
				get { return _partition; }
			}

			public long Size {
				get { return _size; }
			}

			public PartitionMeasured(
				Guid subscriptionId, string partition, long size, long subscriptionMessageSequenceNumber,
				object source = null)
				: base(subscriptionId, null, 100.0f, subscriptionMessageSequenceNumber, source) {
				_partition = partition;
				_size = size;
			}
		}

		/// <summary>
		/// NOTEL the PartitionDeleted may appear out-of-order and is not guaranteed
		/// to appear at the same sequence position in a recovery 
		/// </summary>
		public class PartitionDeleted : EventReaderSubscriptionMessageBase {
			private readonly string _partition;
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public string Partition {
				get { return _partition; }
			}

			public PartitionDeleted(
				Guid subscriptionId, CheckpointTag checkpointTag, string partition,
				long subscriptionMessageSequenceNumber, object source = null)
				: base(subscriptionId, checkpointTag, 100.0f, subscriptionMessageSequenceNumber, source) {
				_partition = partition;
			}
		}

		public class CommittedEventReceived : EventReaderSubscriptionMessageBase {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public static CommittedEventReceived Sample(
				ResolvedEvent data, Guid subscriptionId, long subscriptionMessageSequenceNumber) {
				return new CommittedEventReceived(
					subscriptionId, 0, null, data, 77.7f, subscriptionMessageSequenceNumber);
			}

			public static CommittedEventReceived Sample(
				ResolvedEvent data, CheckpointTag checkpointTag, Guid subscriptionId,
				long subscriptionMessageSequenceNumber) {
				return new CommittedEventReceived(
					subscriptionId, checkpointTag, null, data, 77.7f, subscriptionMessageSequenceNumber, null);
			}

			private readonly ResolvedEvent _data;

			private readonly string _eventCategory;

			private CommittedEventReceived(
				Guid subscriptionId, CheckpointTag checkpointTag, string eventCategory, ResolvedEvent data,
				float progress, long subscriptionMessageSequenceNumber, object source)
				: base(subscriptionId, checkpointTag, progress, subscriptionMessageSequenceNumber, source) {
				if (data == null) throw new ArgumentNullException("data");
				_data = data;
				_eventCategory = eventCategory;
			}

			private CommittedEventReceived(
				Guid subscriptionId, int phase, string eventCategory, ResolvedEvent data, float progress,
				long subscriptionMessageSequenceNumber)
				: this(
					subscriptionId,
					CheckpointTag.FromPosition(phase, data.Position.CommitPosition, data.Position.PreparePosition),
					eventCategory, data, progress, subscriptionMessageSequenceNumber, null) {
			}

			public ResolvedEvent Data {
				get { return _data; }
			}

			public string EventCategory {
				get { return _eventCategory; }
			}

			public static CommittedEventReceived FromCommittedEventDistributed(
				ReaderSubscriptionMessage.CommittedEventDistributed message, CheckpointTag checkpointTag,
				string eventCategory, Guid subscriptionId, long subscriptionMessageSequenceNumber) {
				return new CommittedEventReceived(
					subscriptionId, checkpointTag, eventCategory, message.Data, message.Progress,
					subscriptionMessageSequenceNumber, message.Source);
			}

			public override string ToString() {
				return CheckpointTag.ToString();
			}
		}

		public class ReaderAssignedReader : EventReaderSubscriptionMessageBase {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			private readonly Guid _readerId;

			public ReaderAssignedReader(Guid subscriptionId, Guid readerId)
				: base(subscriptionId, null, 0, 0, null) {
				_readerId = readerId;
			}

			public Guid ReaderId {
				get { return _readerId; }
			}
		}
	}

	public abstract class EventReaderSubscriptionMessageBase : Message {
		private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

		public override int MsgTypeId {
			get { return TypeId; }
		}

		private readonly Guid _subscriptionId;
		private readonly long _subscriptionMessageSequenceNumber;
		private readonly object _source;
		private readonly CheckpointTag _checkpointTag;
		private readonly float _progress;

		internal EventReaderSubscriptionMessageBase(Guid subscriptionId, CheckpointTag checkpointTag, float progress,
			long subscriptionMessageSequenceNumber, object source) {
			_subscriptionId = subscriptionId;
			_checkpointTag = checkpointTag;
			_progress = progress;
			_subscriptionMessageSequenceNumber = subscriptionMessageSequenceNumber;
			_source = source;
		}


		public CheckpointTag CheckpointTag {
			get { return _checkpointTag; }
		}

		public float Progress {
			get { return _progress; }
		}

		public long SubscriptionMessageSequenceNumber {
			get { return _subscriptionMessageSequenceNumber; }
		}

		public Guid SubscriptionId {
			get { return _subscriptionId; }
		}

		public object Source {
			get { return _source; }
		}
	}
}
