extern alias GrpcClient;
using GrpcClient::EventStore.Client;

#nullable enable

namespace EventStore.Core.Tests.ClientAPI.Helpers;

public record ConditionalWriteResult(ConditionalWriteStatus Status, WriteResult? Result) {
	public Position? LogPosition => Result?.LogPosition;
	public long? NextExpectedVersion => Result?.NextExpectedVersion;
}
