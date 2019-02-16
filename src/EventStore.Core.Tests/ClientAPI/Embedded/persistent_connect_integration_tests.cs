using EventStore.ClientAPI;
using NUnit.Framework;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;

namespace EventStore.Core.Tests.ClientAPI.Embedded {
	public class
		happy_case_writing_and_subscribing_to_normal_events_manual_ack : ClientAPI.
			happy_case_writing_and_subscribing_to_normal_events_manual_ack {
		protected override IEventStoreConnection BuildConnection(MiniNode node) {
			return EmbeddedTestConnection.To(node);
		}
	}

	public class
		happy_case_writing_and_subscribing_to_normal_events_auto_ack : ClientAPI.
			happy_case_writing_and_subscribing_to_normal_events_auto_ack {
		protected override IEventStoreConnection BuildConnection(MiniNode node) {
			return EmbeddedTestConnection.To(node);
		}
	}

	public class
		happy_case_catching_up_to_normal_events_auto_ack : ClientAPI.happy_case_catching_up_to_normal_events_auto_ack {
		protected override IEventStoreConnection BuildConnection(MiniNode node) {
			return EmbeddedTestConnection.To(node);
		}
	}

	public class
		happy_case_catching_up_to_normal_events_manual_ack : ClientAPI.
			happy_case_catching_up_to_normal_events_manual_ack {
		protected override IEventStoreConnection BuildConnection(MiniNode node) {
			return EmbeddedTestConnection.To(node);
		}
	}

	public class
		happy_case_catching_up_to_link_to_events_manual_ack : ClientAPI.
			happy_case_catching_up_to_link_to_events_manual_ack {
		protected override IEventStoreConnection BuildConnection(MiniNode node) {
			return EmbeddedTestConnection.To(node);
		}
	}

	public class
		happy_case_catching_up_to_link_to_events_auto_ack : ClientAPI.
			happy_case_catching_up_to_link_to_events_auto_ack {
		protected override IEventStoreConnection BuildConnection(MiniNode node) {
			return EmbeddedTestConnection.To(node);
		}
	}

	public class
		when_writing_and_subscribing_to_normal_events_manual_nack : ClientAPI.
			when_writing_and_subscribing_to_normal_events_manual_nack {
		protected override IEventStoreConnection BuildConnection(MiniNode node) {
			return EmbeddedTestConnection.To(node);
		}
	}
}
