extern alias GrpcClient;
extern alias GrpcClientStreams;
using GrpcClient::EventStore.Client;
using Direction = GrpcClientStreams::EventStore.Client.Direction;

namespace EventStore.Core.Tests.ClientAPI.Helpers;

extern alias GrpcClientStreams;

public record StreamEventsSliceNew(
	string Stream,
	Direction ReadDirection,
	long FromEventNumber,
	long NextEventNumber,
	long LastEventNumber,
	bool IsEndOfStream,
	ResolvedEvent[] Events);
