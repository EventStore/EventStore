namespace EventStore.Core;

// Still needed for gossip and stats.
public class NodeTcpOptions {
	public int NodeTcpPort { get; init; } = 1113;
	public bool EnableExternalTcp { get; init; }
	public int? NodeTcpPortAdvertiseAs { get; init; }
}
