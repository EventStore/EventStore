using EventStore.ClientAPI;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;

namespace EventStore.Core.Tests.ClientAPI.Embedded {
	public class isjson_flag_on_event : ClientAPI.isjson_flag_on_event {
		protected override IEventStoreConnection BuildConnection(MiniNode node) {
			return EmbeddedTestConnection.To(node);
		}
	}
}
