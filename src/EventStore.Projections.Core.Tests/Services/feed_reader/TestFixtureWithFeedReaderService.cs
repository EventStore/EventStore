using EventStore.Projections.Core.EventReaders.Feeds;
using EventStore.Projections.Core.Tests.Services.event_reader;

namespace EventStore.Projections.Core.Tests.Services.feed_reader {
	public abstract class TestFixtureWithFeedReaderService : TestFixtureWithEventReaderService {
		protected FeedReaderService _feedReaderService;

		protected override void Given1() {
			base.Given1();
			EnableReadAll();
		}

		protected override void GivenAdditionalServices() {
			_feedReaderService = new FeedReaderService(_subscriptionDispatcher, _timeProvider);
			_bus.Subscribe(_feedReaderService);
		}
	}
}
