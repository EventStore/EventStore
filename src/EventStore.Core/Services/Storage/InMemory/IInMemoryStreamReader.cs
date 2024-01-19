using EventStore.Core.Messages;

namespace EventStore.Core.Services.Storage.InMemory;

public interface IInMemoryStreamReader {
	ClientMessage.ReadStreamEventsForwardCompleted ReadForwards(ClientMessage.ReadStreamEventsForward msg);
	ClientMessage.ReadStreamEventsBackwardCompleted ReadBackwards(ClientMessage.ReadStreamEventsBackward msg);
}
