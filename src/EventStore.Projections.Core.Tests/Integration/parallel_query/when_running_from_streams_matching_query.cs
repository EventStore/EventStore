using System.Linq;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Management;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Integration.parallel_query {
	[TestFixture]
	public class when_running_from_streams_matching_query : specification_with_a_v8_query_posted {
		protected override void GivenEvents() {
			ExistingEvent("account-01", "test", "", "{a:2}");
			ExistingEvent("account-01", "test", "", "{a:3}");
			ExistingEvent("account-02", "test", "", "{a:5}");
			ExistingEvent("account-03", "test", "", "{a:7}");
			ExistingEvent("account-03", "test", "", "{a:8}");
			ExistingEvent("account-04", "test", "", "{a:11}");
			ExistingEvent("account-05", "test", "", "{a:13}");

			ExistingEvent("$$account-01", SystemEventTypes.StreamMetadata, "", "{\"Skip\": 1}");
			ExistingEvent("$$account-02", SystemEventTypes.StreamMetadata, "", "{\"Skip\": 0}");
			ExistingEvent("$$account-05", SystemEventTypes.StreamMetadata, "", "{\"Skip\": 1}");

			ExistingEvent("$streams", SystemEventTypes.StreamReference, "", "account-01");
			ExistingEvent("$streams", SystemEventTypes.StreamReference, "", "account-02");
			ExistingEvent("$streams", SystemEventTypes.StreamReference, "", "account-03");
			ExistingEvent("$streams", SystemEventTypes.StreamReference, "", "account-04");
			ExistingEvent("$streams", SystemEventTypes.StreamReference, "", "account-05");
		}

		protected override string GivenQuery() {
			return @"
fromStreamsMatching(
    function(streamId, ev){
        return !ev.streamMetadata.Skip;
    }
).when({
    $init: function() { return {c: 0}; },
    $any: function(s, e) { return {c: s.c + 1}; }
})
";
		}

		[Test]
		public void just() {
			AssertEmptyOrNoStream("$projections-query-account-01-result");
			AssertLastEvent("$projections-query-account-02-result", "{\"c\":1}");
			AssertLastEvent("$projections-query-account-03-result", "{\"c\":2}");
			AssertLastEvent("$projections-query-account-04-result", "{\"c\":1}");
			AssertEmptyOrNoStream("$projections-query-account-05-result");
		}

		[Test]
		public void state_becomes_completed() {
			_manager.Handle(
				new ProjectionManagementMessage.Command.GetStatistics(
					new PublishEnvelope(_bus), null, _projectionName, false));

			Assert.AreEqual(1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>().Count());
			Assert.AreEqual(
				1,
				_consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>()
					.Single()
					.Projections.Length);
			Assert.AreEqual(
				_projectionName,
				_consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>()
					.Single()
					.Projections.Single()
					.Name);
			Assert.AreEqual(
				ManagedProjectionState.Completed,
				_consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>()
					.Single()
					.Projections.Single()
					.MasterStatus);
		}
	}
}
