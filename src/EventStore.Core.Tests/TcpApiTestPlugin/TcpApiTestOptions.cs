namespace EventStore.TcpUnitTestPlugin;

public class TcpApiTestOptions {
	public int NodeTcpPort { get; init; } = 1113;
	public int NodeHeartbeatInterval { get; init; } = 2_000;
	public int NodeHeartbeatTimeout { get; init; } = 1_000;
	public int ConnectionPendingSendBytesThreshold { get; set; } = 10 * 1_024 * 1_024;
	public int ConnectionQueueSizeThreshold { get; set; } = 50_000;
	public int WriteTimeoutMs { get; set; } = 2_000;
	public bool Insecure { get; init; } = false;
}
