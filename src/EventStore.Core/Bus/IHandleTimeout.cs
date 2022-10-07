namespace EventStore.Core.Bus {

	public interface IHandleTimeout {
		bool HandlesTimeout { get; }
		void Timeout();
	}
}
