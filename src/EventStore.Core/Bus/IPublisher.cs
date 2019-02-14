using EventStore.Core.Messaging;

namespace EventStore.Core.Bus {
	public interface IPublisher {
		void Publish(Message message);
	}

	/// <summary>
	/// Marks <see cref="IPublisher"/> as being OK for
	/// cross-thread publishing (e.g. in replying to envelopes).
	/// </summary>
	public interface IThreadSafePublisher {
	}
}
