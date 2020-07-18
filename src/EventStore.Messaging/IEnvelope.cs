namespace EventStore.Core.Messaging {
	public interface IEnvelope {
		void ReplyWith<T>(T message) where T : Message;
	}
}
