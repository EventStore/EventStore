// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.AutoScavenge.Clients;

public interface IGossipAware {
	public void ReceiveGossipMessage(GossipMessage msg);
}
