using System;

namespace EventStore.Core.Services.Transport.Grpc {
	public partial class PersistentSubscriptions
		: EventStore.Grpc.PersistentSubscriptions.PersistentSubscriptions.PersistentSubscriptionsBase {
		private readonly ClusterVNode _node;

		public PersistentSubscriptions(ClusterVNode node) {
			if (node == null) {
				throw new ArgumentNullException(nameof(node));
			}

			_node = node;
		}
	}
}
