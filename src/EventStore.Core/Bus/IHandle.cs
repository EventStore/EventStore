using EventStore.Core.Diagnostics;
using EventStore.Core.Messaging;

namespace EventStore.Core.Bus {
	public interface IHandle<T> where T : Message {
		void Handle(T message);
	}

	//qq name/location
	public interface IHandleEx<T> where T : Message {
		void Handle(StatInfo info, T message);
	}
}
