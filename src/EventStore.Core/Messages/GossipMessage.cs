// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Net;
using EventStore.Core.Cluster;
using EventStore.Core.Messaging;

namespace EventStore.Core.Messages;

public static partial class GossipMessage {
	[DerivedMessage(CoreMessage.Gossip)]
	public partial class RetrieveGossipSeedSources : Message;

	[DerivedMessage(CoreMessage.Gossip)]
	public partial class GotGossipSeedSources(EndPoint[] gossipSeeds) : Message {
		public readonly EndPoint[] GossipSeeds = gossipSeeds;
	}

	[DerivedMessage(CoreMessage.Gossip)]
	public partial class Gossip(int gossipRound) : Message {
		public readonly int GossipRound = gossipRound;
	}

	[DerivedMessage(CoreMessage.Gossip)]
	public partial class GossipReceived(IEnvelope envelope, ClusterInfo clusterInfo, EndPoint server) : Message {
		public readonly IEnvelope Envelope = envelope;
		public readonly ClusterInfo ClusterInfo = clusterInfo;
		public readonly EndPoint Server = server;
	}

	[DerivedMessage(CoreMessage.Gossip)]
	public partial class ReadGossip(IEnvelope envelope) : Message {
		public readonly IEnvelope Envelope = envelope;
	}

	[DerivedMessage(CoreMessage.Gossip)]
	public partial class SendGossip(ClusterInfo clusterInfo, EndPoint serverEndPoint) : Message {
		public readonly ClusterInfo ClusterInfo = clusterInfo;
		public readonly EndPoint ServerEndPoint = serverEndPoint;
	}

	[DerivedMessage(CoreMessage.Gossip)]
	public partial class ClientGossip(IEnvelope envelope) : Message {
		public readonly IEnvelope Envelope = envelope;
	}

	[DerivedMessage(CoreMessage.Gossip)]
	public partial class SendClientGossip(ClientClusterInfo clusterInfo) : Message {
		public readonly ClientClusterInfo ClusterInfo = clusterInfo;
	}

	[DerivedMessage(CoreMessage.Gossip)]
	public partial class GossipUpdated(ClusterInfo clusterInfo) : Message {
		public readonly ClusterInfo ClusterInfo = clusterInfo;
	}

	[DerivedMessage(CoreMessage.Gossip)]
	public partial class GossipSendFailed(string reason, EndPoint recipient) : Message {
		public readonly string Reason = reason;
		public readonly EndPoint Recipient = recipient;

		public override string ToString() => $"Reason: {Reason}, Recipient: {Recipient}";
	}

	[DerivedMessage(CoreMessage.Gossip)]
	public partial class GetGossip : Message;

	[DerivedMessage(CoreMessage.Gossip)]
	public partial class GetGossipFailed(string reason, EndPoint recipient) : Message {
		public readonly string Reason = reason;
		public readonly EndPoint Recipient = recipient;

		public override string ToString() => $"Reason: {Reason}, Recipient: {Recipient}";
	}

	[DerivedMessage(CoreMessage.Gossip)]
	public partial class GetGossipReceived(ClusterInfo clusterInfo, EndPoint server) : Message {
		public readonly ClusterInfo ClusterInfo = clusterInfo;
		public readonly EndPoint Server = server;
	}

	[DerivedMessage(CoreMessage.Gossip)]
	public partial class UpdateNodePriority(int priority) : Message {
		public readonly int NodePriority = priority;
	}
}
