using System;
using EventStore.Core.Bus;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services {
	public sealed class ReaderSubscriptionDispatcher :
		PublishSubscribeDispatcher
		<Guid, ReaderSubscriptionManagement.Subscribe,
			ReaderSubscriptionManagement.ReaderSubscriptionManagementMessage, EventReaderSubscriptionMessageBase> {
		public ReaderSubscriptionDispatcher(IPublisher publisher)
			: base(publisher, v => v.SubscriptionId, v => v.SubscriptionId) {
		}
	}
}
