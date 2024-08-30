using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing.Subscriptions;

namespace EventStore.Projections.Core.Services.Processing.TransactionFile;

public partial class HeadingEventReader
{
	private class PartitionDeletedItem : Item {
		public readonly ReaderSubscriptionMessage.EventReaderPartitionDeleted Message;

		public PartitionDeletedItem(ReaderSubscriptionMessage.EventReaderPartitionDeleted message)
			: base(message.DeleteLinkOrEventPosition.Value) {
			Message = message;
		}

		public override void Handle(IReaderSubscription subscription) {
			subscription.Handle(Message);
		}
	}
}
