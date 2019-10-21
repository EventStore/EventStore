using System;

namespace EventStore.Core.Services.Transport.Grpc {
	public partial class Streams : EventStore.Grpc.Streams.Streams.StreamsBase {
		private readonly ClusterVNode _node;

		public Streams(ClusterVNode node) {
			if (node == null) {
				throw new ArgumentNullException(nameof(node));
			}

			_node = node;
		}
	}
}
