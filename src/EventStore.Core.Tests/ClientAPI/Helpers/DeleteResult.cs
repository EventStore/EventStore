extern alias GrpcClient;
using GrpcClient::EventStore.Client;

namespace EventStore.Core.Tests.ClientAPI.Helpers;

public record DeleteResult(long NextEventNumber, Position Position);
