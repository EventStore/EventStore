namespace EventStore.Core.Tests.Services.Replication.LogReplication;

internal record ReplicationAck {
	public long ReplicationPosition { get; init; }
	public long WriterPosition { get; init; }
}
