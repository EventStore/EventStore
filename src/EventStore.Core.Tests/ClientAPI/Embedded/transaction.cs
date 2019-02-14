using EventStore.ClientAPI;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;

namespace EventStore.Core.Tests.ClientAPI.Embedded {
	public class transaction : ClientAPI.transaction {
		protected override IEventStoreConnection BuildConnection(MiniNode node) {
			return EmbeddedTestConnection.To(node);
		}
	}
}
