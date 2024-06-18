using EventStore.Core.Messages;

namespace EventStore.Core.Services.Storage;

public interface IWritesTracker {
	void OnWrite(ClientMessage.WriteEvents msg);
}
