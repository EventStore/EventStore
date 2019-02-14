using System;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Messages {
	public static class ReaderSubscriptionManagement {
		public abstract class ReaderSubscriptionManagementMessage : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			private readonly Guid _subscriptionId;

			protected ReaderSubscriptionManagementMessage(Guid subscriptionId) {
				_subscriptionId = subscriptionId;
			}

			public Guid SubscriptionId {
				get { return _subscriptionId; }
			}
		}

		public class Subscribe : ReaderSubscriptionManagementMessage {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			private readonly CheckpointTag _fromPosition;
			private readonly IReaderStrategy _readerStrategy;
			private readonly ReaderSubscriptionOptions _options;

			public Subscribe(
				Guid subscriptionId, CheckpointTag from,
				IReaderStrategy readerStrategy, ReaderSubscriptionOptions readerSubscriptionOptions) : base(
				subscriptionId) {
				if (@from == null) throw new ArgumentNullException("from");
				if (readerStrategy == null) throw new ArgumentNullException("readerStrategy");
				_fromPosition = @from;
				_readerStrategy = readerStrategy;
				_options = readerSubscriptionOptions;
			}

			public CheckpointTag FromPosition {
				get { return _fromPosition; }
			}

			public IReaderStrategy ReaderStrategy {
				get { return _readerStrategy; }
			}

			public ReaderSubscriptionOptions Options {
				get { return _options; }
			}
		}

		public class Pause : ReaderSubscriptionManagementMessage {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public Pause(Guid subscriptionId)
				: base(subscriptionId) {
			}
		}

		public class Resume : ReaderSubscriptionManagementMessage {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public Resume(Guid subscriptionId)
				: base(subscriptionId) {
			}
		}

		public class Unsubscribe : ReaderSubscriptionManagementMessage {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public Unsubscribe(Guid subscriptionId)
				: base(subscriptionId) {
			}
		}


		public sealed class SpoolStreamReading : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public long LimitingCommitPosition {
				get { return _limitingCommitPosition; }
			}

			public Guid WorkerId {
				get { return _workerId; }
			}

			private readonly Guid _workerId;
			public readonly Guid SubscriptionId;
			public readonly string StreamId;
			public readonly long CatalogSequenceNumber;
			private readonly long _limitingCommitPosition;

			public SpoolStreamReading(
				Guid workerId,
				Guid subscriptionId,
				string streamId,
				long catalogSequenceNumber,
				long limitingCommitPosition) {
				_workerId = workerId;
				SubscriptionId = subscriptionId;
				StreamId = streamId;
				CatalogSequenceNumber = catalogSequenceNumber;
				_limitingCommitPosition = limitingCommitPosition;
			}
		}

		public sealed class CompleteSpooledStreamReading : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid SubscriptionId;

			public CompleteSpooledStreamReading(Guid subscriptionId) {
				SubscriptionId = subscriptionId;
			}
		}

		public sealed class SpoolStreamReadingCore : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public long LimitingCommitPosition {
				get { return _limitingCommitPosition; }
			}

			public readonly Guid SubscriptionId;
			public readonly string StreamId;
			public readonly long CatalogSequenceNumber;
			private readonly long _limitingCommitPosition;

			public SpoolStreamReadingCore(
				Guid subscriptionId,
				string streamId,
				long catalogSequenceNumber,
				long limitingCommitPosition) {
				SubscriptionId = subscriptionId;
				StreamId = streamId;
				CatalogSequenceNumber = catalogSequenceNumber;
				_limitingCommitPosition = limitingCommitPosition;
			}
		}
	}
}
