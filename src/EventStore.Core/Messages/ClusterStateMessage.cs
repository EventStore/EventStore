using System.Collections.Generic;
using System.Net;
using EventStore.Core.Messaging;

namespace EventStore.Core.Messages {
	public static partial class ClusterStateMessage {
		[DerivedMessage(CoreMessage.ClusterState)]
		public partial class MultipleVersionsOnNodes : Message {
			public Dictionary<EndPoint, string> NodeHTTPEndpointVsVersion { get; }
		
			public MultipleVersionsOnNodes(Dictionary<EndPoint, string> NodeHTTPEndpointVsVersion) {
				this.NodeHTTPEndpointVsVersion = NodeHTTPEndpointVsVersion;
			}
		}
	}
}
