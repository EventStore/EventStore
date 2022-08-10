namespace EventStore.Core.TransactionLog.Scavenging {
	public enum CollisionResult {
		NoCollision,
		NewCollision, // collided with something that was not previously in a collision
		OldCollision, // collided with something that was already in a collision
	}
}
