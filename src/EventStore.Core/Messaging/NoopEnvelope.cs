namespace EventStore.Core.Messaging {
	public class NoopEnvelope : IEnvelope {
		public void ReplyWith<T>(T message) where T : Message {
		}
	}
}
