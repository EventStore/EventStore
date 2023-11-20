extern alias GrpcClient;
using GrpcClient::EventStore.Client;

namespace EventStore.Core.Tests.ClientAPI.Helpers;

public record EventReadResultNew(EventReadStatus Status, string Stream, long EventNumber, ResolvedEvent? Event);
