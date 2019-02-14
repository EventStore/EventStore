using EventStore.ClientAPI;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;

namespace EventStore.Core.Tests.ClientAPI.Embedded {
	public class read_stream_events_with_unresolved_linkto : ClientAPI.read_stream_events_with_unresolved_linkto {
		protected override IEventStoreConnection BuildConnection(MiniNode node) {
			return EmbeddedTestConnection.To(node);
		}
	}
}
