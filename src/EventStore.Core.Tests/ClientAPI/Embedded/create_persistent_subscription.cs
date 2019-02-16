using EventStore.ClientAPI;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Embedded {
	[TestFixture, Category("LongRunning")]
	public class create_persistent_subscription_on_existing_stream :
		ClientAPI.create_persistent_subscription_on_existing_stream {
		protected override IEventStoreConnection BuildConnection(MiniNode node) {
			return EmbeddedTestConnection.To(node);
		}
	}

	[TestFixture, Category("LongRunning")]
	public class create_persistent_subscription_on_non_existing_stream :
		ClientAPI.create_persistent_subscription_on_non_existing_stream {
		protected override IEventStoreConnection BuildConnection(MiniNode node) {
			return EmbeddedTestConnection.To(node);
		}
	}

	[TestFixture, Category("LongRunning")]
	public class create_duplicate_persistent_subscription_group :
		ClientAPI.create_duplicate_persistent_subscription_group {
		protected override IEventStoreConnection BuildConnection(MiniNode node) {
			return EmbeddedTestConnection.To(node);
		}
	}

	[TestFixture, Category("LongRunning")]
	public class can_create_duplicate_persistent_subscription_group_name_on_different_streams :
		ClientAPI.can_create_duplicate_persistent_subscription_group_name_on_different_streams {
		protected override IEventStoreConnection BuildConnection(MiniNode node) {
			return EmbeddedTestConnection.To(node);
		}
	}

	[TestFixture, Category("LongRunning")]
	public class create_persistent_subscription_group_without_permissions :
		ClientAPI.create_persistent_subscription_group_without_permissions {
		protected override IEventStoreConnection BuildConnection(MiniNode node) {
			return EmbeddedTestConnection.To(node);
		}
	}
}
