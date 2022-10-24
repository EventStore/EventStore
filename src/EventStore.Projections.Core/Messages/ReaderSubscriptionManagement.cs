using System;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Messages {
	public static partial class ReaderSubscriptionManagement {

		[StatsGroup("projections-reader-subscription-management")]
		public enum MessageType {
			None = 0,
			Subscribe = 1,
			Pause = 2,
			Resume = 3,
			Unsubscribe = 4,
		}

		[StatsMessage]
		public abstract partial class ReaderSubscriptionManagementMessage : Message {
			private readonly Guid _subscriptionId;

			protected ReaderSubscriptionManagementMessage(Guid subscriptionId) {
				_subscriptionId = subscriptionId;
			}

			public Guid SubscriptionId {
				get { return _subscriptionId; }
			}
		}

		[StatsMessage(MessageType.Subscribe)]
		public partial class Subscribe : ReaderSubscriptionManagementMessage {
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

		[StatsMessage(MessageType.Pause)]
		public partial class Pause : ReaderSubscriptionManagementMessage {
			public Pause(Guid subscriptionId)
				: base(subscriptionId) {
			}
		}

		[StatsMessage(MessageType.Resume)]
		public partial class Resume : ReaderSubscriptionManagementMessage {
			public Resume(Guid subscriptionId)
				: base(subscriptionId) {
			}
		}

		[StatsMessage(MessageType.Unsubscribe)]
		public partial class Unsubscribe : ReaderSubscriptionManagementMessage {
			public Unsubscribe(Guid subscriptionId)
				: base(subscriptionId) {
			}
		}
	}
}
