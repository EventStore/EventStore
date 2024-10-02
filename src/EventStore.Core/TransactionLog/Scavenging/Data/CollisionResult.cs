// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.TransactionLog.Scavenging {
	public enum CollisionResult {
		NoCollision,
		NewCollision, // collided with something that was not previously in a collision
		OldCollision, // collided with something that was already in a collision
	}
}
