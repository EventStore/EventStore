extern alias GrpcClient;
extern alias GrpcClientStreams;
using GrpcClient::EventStore.Client;
using Direction = GrpcClientStreams::EventStore.Client.Direction;

namespace EventStore.Core.Tests.ClientAPI.Helpers;

extern alias GrpcClientStreams;

public class StreamEventsSliceNew {
	public SliceReadStatus Status { get; private set; }
	public string Stream { get; private set; }
	public Direction ReadDirection { get; private set; }
	public long FromEventNumber { get; private set; }
	public long NextEventNumber { get; private set; }
	public long LastEventNumber { get; private set; }
	public bool IsEndOfStream { get; private set; }
	public ResolvedEvent[] Events { get; private set; }
	
	public StreamEventsSliceNew(string stream, Direction readDirection, long fromEventNumber, long nextEventNumber, long lastEventNumber, bool isEndOfStream, ResolvedEvent[] events) {
		Status = SliceReadStatus.Success;
		Stream = stream;
		ReadDirection = readDirection;
		FromEventNumber = fromEventNumber;
		NextEventNumber = nextEventNumber;
		LastEventNumber = lastEventNumber;
		IsEndOfStream = isEndOfStream;
		Events = events;
	}

	public StreamEventsSliceNew(SliceReadStatus status, Direction direction) {
		Status = status;
		Events = new ResolvedEvent[0];
		IsEndOfStream = true;
		ReadDirection = direction;
	}
}
