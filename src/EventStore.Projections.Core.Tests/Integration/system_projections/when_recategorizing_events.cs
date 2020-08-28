﻿using System.Collections.Generic;
using EventStore.Core.Services;
using EventStore.Core.TransactionLog.Services;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Integration.system_projections {
	[TestFixture]
	public class when_recategorizing_events : specification_with_a_v8_query_posted {
		protected override void GivenEvents() {
			ExistingEvent("account-01", "test", "", "{\"a\":1}", isJson: true);
			ExistingEvent("account-01", "test", "", "{\"a\":2}", isJson: true);
			ExistingEvent("account-02", "test", "", "{\"a\":10}", isJson: true);

			ExistingEvent("categorized-0", SystemEventTypes.LinkTo, "{\"a\":2}", "1@account-01");
			ExistingEvent("categorized-0", SystemEventTypes.LinkTo, "{\"a\":10}", "0@account-02");
			ExistingEvent("categorized-1", SystemEventTypes.LinkTo, "{\"a\":1}", "0@account-01");
		}

		protected override IEnumerable<WhenStep> When() {
			foreach (var e in base.When()) yield return e;
		}

		protected override bool GivenInitializeSystemProjections() {
			return true;
		}

		protected override bool GivenStartSystemProjections() {
			return true;
		}

		protected override string GivenQuery() {
			return "";
		}

		[Test]
		public void streams_are_categorized() {
			AssertStreamTail("$category-account", "account-01", "account-02");
			AssertStreamTail("$category-categorized", "categorized-0", "categorized-1");
		}

		[Test]
		public void events_are_categorized() {
			AssertStreamTail("$ce-account", "0@account-01", "1@account-01", "0@account-02");
		}

		[Test]
		public void links_are_categorized() {
			AssertStreamTail("$ce-categorized", "1@account-01", "0@account-02", "0@account-01");
		}
	}
}
