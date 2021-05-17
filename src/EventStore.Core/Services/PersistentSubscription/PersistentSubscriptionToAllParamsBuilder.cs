using System;
using EventStore.Common.Utils;
using EventStore.Core.Services.PersistentSubscription.ConsumerStrategy;

namespace EventStore.Core.Services.PersistentSubscription {
	/// <summary>
	/// Builds a <see cref="PersistentSubscriptionParams"/> object.
	/// </summary>
	public class PersistentSubscriptionToAllParamsBuilder : PersistentSubscriptionParamsBuilder {
		/// <summary>
		/// Creates a new <see cref="PersistentSubscriptionParamsBuilder"></see> object
		/// </summary>
		/// <param name="streamName">The name of the stream for the subscription</param>
		/// <param name="groupName">The name of the group of the subscription</param>
		/// <returns>a new <see cref="PersistentSubscriptionParamsBuilder"></see> object</returns>
		public static PersistentSubscriptionParamsBuilder CreateFor(string groupName) {
			return new PersistentSubscriptionToAllParamsBuilder()
				.FromAll()
				.StartFrom(0L, 0L)
				.SetGroup(groupName)
				.SetSubscriptionId("$all" + ":" + groupName)
				.DoNotResolveLinkTos()
				.WithMessageTimeoutOf(TimeSpan.FromSeconds(30))
				.WithHistoryBufferSizeOf(500)
				.WithLiveBufferSizeOf(500)
				.WithMaxRetriesOf(10)
				.WithReadBatchOf(20)
				.CheckPointAfter(TimeSpan.FromSeconds(1))
				.MinimumToCheckPoint(5)
				.MaximumToCheckPoint(1000)
				.MaximumSubscribers(0)
				.WithNamedConsumerStrategy(new RoundRobinPersistentSubscriptionConsumerStrategy());
		}

		public PersistentSubscriptionToAllParamsBuilder FromAll() {
			WithEventSource(new PersistentSubscriptionAllStreamEventSource());
			return this;
		}

		public PersistentSubscriptionToAllParamsBuilder StartFrom(long commitPosition, long preparePosition) {
			StartFrom(new PersistentSubscriptionAllStreamPosition(commitPosition, preparePosition));
			return this;
		}

		public override PersistentSubscriptionParamsBuilder StartFromBeginning() {
			StartFrom(new PersistentSubscriptionAllStreamPosition(0, 0));
			return this;
		}

		public override PersistentSubscriptionParamsBuilder StartFromCurrent() {
			StartFrom(new PersistentSubscriptionAllStreamPosition(-1, -1));
			return this;
		}

	}
}
