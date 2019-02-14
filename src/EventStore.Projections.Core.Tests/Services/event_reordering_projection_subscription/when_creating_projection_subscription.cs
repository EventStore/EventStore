using System;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Services.TimeService;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.event_reordering_projection_subscription {
	[TestFixture]
	public class when_creating_projection_subscription {
		[Test]
		public void it_can_be_created() {
			new EventReorderingReaderSubscription(
				new FakePublisher(),
				Guid.NewGuid(),
				CheckpointTag.FromPosition(0, 0, -1),
				CreateReaderStrategy(),
				new FakeTimeProvider(),
				1000,
				2000,
				10000,
				500);
		}

		[Test]
		public void null_publisher_throws_argument_null_exception() {
			Assert.Throws<ArgumentNullException>(() => {
				new EventReorderingReaderSubscription(
					null,
					Guid.NewGuid(),
					CheckpointTag.FromPosition(0, 0, -1),
					CreateReaderStrategy(),
					new FakeTimeProvider(),
					1000,
					2000,
					10000,
					500);
			});
		}

		[Test]
		public void null_describe_source_throws_argument_null_exception() {
			Assert.Throws<ArgumentNullException>(() => {
				new EventReorderingReaderSubscription(
					new FakePublisher(),
					Guid.NewGuid(),
					CheckpointTag.FromPosition(0, 0, -1),
					null,
					new FakeTimeProvider(),
					1000,
					2000,
					10000,
					500);
			});
		}

		[Test]
		public void null_time_provider_throws_argument_null_exception() {
			Assert.Throws<ArgumentNullException>(() => {
				new EventReorderingReaderSubscription(
					new FakePublisher(),
					Guid.NewGuid(),
					CheckpointTag.FromPosition(0, 0, -1),
					CreateReaderStrategy(),
					null,
					1000,
					2000,
					10000,
					500);
			});
		}

		private IReaderStrategy CreateReaderStrategy() {
			var result = new SourceDefinitionBuilder();
			result.FromAll();
			result.AllEvents();
			return ReaderStrategy.Create(
				"test",
				0,
				result.Build(),
				new RealTimeProvider(),
				stopOnEof: false,
				runAs: null);
		}
	}
}
