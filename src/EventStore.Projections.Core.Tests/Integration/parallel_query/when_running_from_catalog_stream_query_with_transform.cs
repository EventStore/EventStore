using System.Linq;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Management;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Integration.parallel_query {
	[TestFixture]
	public class when_running_from_catalog_stream_query_with_transform : specification_with_a_v8_query_posted {
		protected override void GivenEvents() {
			ExistingEvent("catalog", "A", "", "{\"a\":\"01\"}", isJson: true);
			ExistingEvent("catalog", "A", "", "{\"a\":\"02\"}", isJson: true);
			ExistingEvent("catalog", "A", "", "{\"a\":\"03\"}", isJson: true);

			ExistingEvent("account-01", "test", "", "{}");
			ExistingEvent("account-01", "test", "", "{}");
			ExistingEvent("account-03", "test", "", "{}");
			ExistingEvent("account-03", "test", "", "{}");
			ExistingEvent("account-03", "test", "", "{}");
		}

		protected override string GivenQuery() {
			return @"
fromStreamCatalog('catalog', function(ev) {log(JSON.stringify(ev)); return 'account-' + ev.body.a;}).foreachStream().when({
    $init: function() { return {c: 0}; },
    $any: function(s, e) { return {c: s.c + 1}; }
})
";
		}

		[Test]
		public void just() {
			AssertLastEvent("$projections-query-account-01-result", "{\"c\":2}");
//            AssertLastEvent("$projections-query-account-02-result", "{\"c\":0}");
			AssertLastEvent("$projections-query-account-03-result", "{\"c\":3}");
		}

		[Test]
		public void state_becomes_completed() {
			_manager.Handle(
				new ProjectionManagementMessage.Command.GetStatistics(new PublishEnvelope(_bus), null, _projectionName,
					false));

			Assert.AreEqual(1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>().Count());
			Assert.AreEqual(
				1,
				_consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>().Single().Projections.Length);
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
