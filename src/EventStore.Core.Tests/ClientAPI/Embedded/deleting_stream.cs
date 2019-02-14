using EventStore.ClientAPI;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;

namespace EventStore.Core.Tests.ClientAPI.Embedded {
	public class deleting_stream : ClientAPI.deleting_stream {
		protected override IEventStoreConnection BuildConnection(MiniNode node) {
			return EmbeddedTestConnection.To(node);
		}
	}
}
