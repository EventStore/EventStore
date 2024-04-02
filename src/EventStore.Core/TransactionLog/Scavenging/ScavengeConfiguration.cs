using System;

namespace EventStore.Core.TransactionLog.Scavenging;

public class ScavengeConfiguration {
	public TimeSpan Schedule { get; set; }
}
