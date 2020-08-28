﻿using System.Linq;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Core.TransactionLog.Services;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Management;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Integration.link_metadata {
	[TestFixture]
	public class when_running_a_query_using_link_metadata : specification_with_a_v8_query_posted {
		protected override void GivenEvents() {
			ExistingEvent("stream", SystemEventTypes.LinkTo, "{\"a\":1}", "0@account-01");
			ExistingEvent("stream", SystemEventTypes.LinkTo, "{\"a\":2}", "1@account-01");
			ExistingEvent("stream", SystemEventTypes.LinkTo, "{\"a\":10}", "0@account-02");

			ExistingEvent("account-01", "test", "", "{\"a\":1}", isJson: true);
			ExistingEvent("account-01", "test", "", "{\"a\":2}", isJson: true);
			ExistingEvent("account-02", "test", "", "{\"a\":10}", isJson: true);
		}

		protected override string GivenQuery() {
			return @"
fromStream('stream').when({
    $any: function(s, e) { 
        // test
        if (JSON.stringify(e.body) != JSON.stringify(e.linkMetadata))
            throw 'invalid link metadata ' + JSON.stringify(e.linkMetadata) + ' expected is ' + JSON.stringify(e.body);
        
        return e.linkMetadata; 
    }
}).outputState()
";
		}

		[Test]
		public void just() {
			AssertLastEvent("$projections-query-result", "{\"a\":10}", skip: 1 /* $eof */);
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
					.LeaderStatus);
		}
	}
}
