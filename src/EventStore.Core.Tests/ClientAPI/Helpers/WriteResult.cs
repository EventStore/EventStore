extern alias GrpcClient;
using GrpcClient::EventStore.Client;

namespace EventStore.Core.Tests.ClientAPI.Helpers;

public record WriteResult(long NextExpectedVersion, Position LogPosition);
