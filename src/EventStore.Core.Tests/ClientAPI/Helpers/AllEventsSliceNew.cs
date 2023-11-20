extern alias GrpcClient;
extern alias GrpcClientStreams;
using Position = GrpcClient::EventStore.Client.Position;
using GrpcClientStreams::EventStore.Client;
using ResolvedEvent = GrpcClient::EventStore.Client.ResolvedEvent;

namespace EventStore.Core.Tests.ClientAPI.Helpers;

public record AllEventsSliceNew(
	Direction ReadDirection,
	Position NextPosition,
	bool IsEndOfStream,
	ResolvedEvent[] Events);

