using EventStore.ClientAPI;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Embedded {
	[TestFixture, Category("LongRunning")]
	public class connect_to_non_existing_persistent_subscription_with_permissions :
		ClientAPI.connect_to_non_existing_persistent_subscription_with_permissions {
		protected override IEventStoreConnection BuildConnection(MiniNode node) {
			return EmbeddedTestConnection.To(node);
		}
	}

	[TestFixture, Category("LongRunning")]
	public class connect_to_existing_persistent_subscription_with_permissions :
		ClientAPI.connect_to_existing_persistent_subscription_with_permissions {
		protected override IEventStoreConnection BuildConnection(MiniNode node) {
			return EmbeddedTestConnection.To(node);
		}
	}

	[TestFixture, Category("LongRunning")]
	public class connect_to_existing_persistent_subscription_without_permissions :
		ClientAPI.connect_to_existing_persistent_subscription_without_permissions {
		protected override IEventStoreConnection BuildConnection(MiniNode node) {
			return EmbeddedTestConnection.To(node);
		}
	}

	[TestFixture, Category("LongRunning")]
	public class connect_to_existing_persistent_subscription_with_start_from_beginning_and_events_in_it :
		ClientAPI.connect_to_existing_persistent_subscription_with_start_from_beginning_and_events_in_it {
		protected override IEventStoreConnection BuildConnection(MiniNode node) {
			return EmbeddedTestConnection.To(node);
		}
	}

	[TestFixture, Category("LongRunning")]
	public class connect_to_existing_persistent_subscription_with_start_from_beginning_not_set_and_events_in_it :
		ClientAPI.connect_to_existing_persistent_subscription_with_start_from_beginning_not_set_and_events_in_it {
		protected override IEventStoreConnection BuildConnection(MiniNode node) {
			return EmbeddedTestConnection.To(node);
		}
	}

	[TestFixture, Category("LongRunning")]
	public class
		connect_to_existing_persistent_subscription_with_start_from_beginning_not_set_and_events_in_it_then_event_written :
			ClientAPI.
			connect_to_existing_persistent_subscription_with_start_from_beginning_not_set_and_events_in_it_then_event_written {
		protected override IEventStoreConnection BuildConnection(MiniNode node) {
			return EmbeddedTestConnection.To(node);
		}
	}

	[TestFixture, Category("LongRunning")]
	public class
		connect_to_existing_persistent_subscription_with_start_from_x_set_higher_than_x_and_events_in_it_then_event_written :
			ClientAPI.
			connect_to_existing_persistent_subscription_with_start_from_x_set_higher_than_x_and_events_in_it_then_event_written {
		protected override IEventStoreConnection BuildConnection(MiniNode node) {
			return EmbeddedTestConnection.To(node);
		}
	}

	[TestFixture, Category("LongRunning")]
	public class connect_to_existing_persistent_subscription_with_start_from_x_set_and_events_in_it_then_event_written :
		ClientAPI.
		connect_to_existing_persistent_subscription_with_start_from_x_set_and_events_in_it_then_event_written {
		protected override IEventStoreConnection BuildConnection(MiniNode node) {
			return EmbeddedTestConnection.To(node);
		}
	}

	[TestFixture, Category("LongRunning")]
	public class connect_to_existing_persistent_subscription_with_start_from_x_set_and_events_in_it :
		ClientAPI.connect_to_existing_persistent_subscription_with_start_from_x_set_and_events_in_it {
		protected override IEventStoreConnection BuildConnection(MiniNode node) {
			return EmbeddedTestConnection.To(node);
		}
	}
}
