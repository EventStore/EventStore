namespace EventStore.Core.Bus {
	public interface IBus : IPublisher, ISubscriber {
		string Name { get; }
	}
}
