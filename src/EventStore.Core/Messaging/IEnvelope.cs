namespace EventStore.Core.Messaging {
	public interface IEnvelope<in T> {
		void ReplyWith<U>(U message) where U : T;
	}

	public interface IEnvelope : IEnvelope<Message> {
	}
}
