using EventStore.ClientAPI;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Embedded {
	[TestFixture, Category("LongRunning")]
	public class deleting_existing_persistent_subscription_group_with_permissions :
		ClientAPI.deleting_existing_persistent_subscription_group_with_permissions {
		protected override IEventStoreConnection BuildConnection(MiniNode node) {
			return EmbeddedTestConnection.To(node);
		}
	}

	[TestFixture, Category("LongRunning")]
	public class deleting_persistent_subscription_group_that_doesnt_exist :
		ClientAPI.deleting_persistent_subscription_group_that_doesnt_exist {
		protected override IEventStoreConnection BuildConnection(MiniNode node) {
			return EmbeddedTestConnection.To(node);
		}
	}

	[TestFixture, Category("LongRunning")]
	public class deleting_persistent_subscription_group_without_permissions :
		ClientAPI.deleting_persistent_subscription_group_without_permissions {
		protected override IEventStoreConnection BuildConnection(MiniNode node) {
			return EmbeddedTestConnection.To(node);
		}
	}
}
