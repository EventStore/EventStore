using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.another_epoch {
	[TestFixture]
	public class when_starting_an_existing_projection : TestFixtureWithCoreProjectionStarted {
		private string _testProjectionState = @"{""test"":1}";

		protected override void Given() {
			_version = new ProjectionVersion(1, 2, 2);
			ExistingEvent(
				"$projections-projection-result", "Result",
				@"{""v"":1, ""c"": 100, ""p"": 50}", _testProjectionState);
			ExistingEvent(
				"$projections-projection-checkpoint", ProjectionEventTypes.ProjectionCheckpoint,
				@"{""v"":1, ""c"": 100, ""p"": 50}", _testProjectionState);
			ExistingEvent(
				"$projections-projection-result", "Result",
				@"{""v"":1, ""c"": 200, ""p"": 150}", _testProjectionState);
			ExistingEvent(
				"$projections-projection-result", "Result",
				@"{""v"":1, ""c"": 300, ""p"": 250}", _testProjectionState);
		}

		protected override void When() {
		}


		[Test]
		public void should_subscribe_from_the_beginning() {
			Assert.AreEqual(1, _subscribeProjectionHandler.HandledMessages.Count);
			Assert.AreEqual(0, _subscribeProjectionHandler.HandledMessages[0].FromPosition.Position.CommitPosition);
			Assert.AreEqual(-1, _subscribeProjectionHandler.HandledMessages[0].FromPosition.Position.PreparePosition);
		}
	}
}
