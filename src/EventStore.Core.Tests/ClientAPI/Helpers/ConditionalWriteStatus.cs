namespace EventStore.Core.Tests.ClientAPI.Helpers;

public enum ConditionalWriteStatus {
	VersionMismatch,
	StreamDeleted,
	Succeeded
}
