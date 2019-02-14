using System;
using EventStore.Core.Bus;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.EventReaders.Feeds;
using EventStore.Projections.Core.Services;

namespace EventStore.Projections.Core.EventReaders.Feeds {
	public class FeedReaderService : IHandle<FeedReaderMessage.ReadPage> {
		private readonly
			PublishSubscribeDispatcher
			<Guid, ReaderSubscriptionManagement.Subscribe,
				ReaderSubscriptionManagement.ReaderSubscriptionManagementMessage, EventReaderSubscriptionMessageBase>
			_subscriptionDispatcher;

		private readonly ITimeProvider _timeProvider;

		public FeedReaderService(
			PublishSubscribeDispatcher
				<Guid, ReaderSubscriptionManagement.Subscribe,
					ReaderSubscriptionManagement.ReaderSubscriptionManagementMessage, EventReaderSubscriptionMessageBase
				>
				subscriptionDispatcher, ITimeProvider timeProvider) {
			_subscriptionDispatcher = subscriptionDispatcher;
			_timeProvider = timeProvider;
		}

		public void Handle(FeedReaderMessage.ReadPage message) {
			var reader = FeedReader.Create(_subscriptionDispatcher, message, _timeProvider);
			reader.Start();
		}
	}
}
