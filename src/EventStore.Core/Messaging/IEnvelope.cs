namespace EventStore.Core.Messaging {
	public interface IEnvelope<in T> {
		void ReplyWith<U>(U message) where U : T;
	}

	public interface IEnvelope : IEnvelope<Message> {
		static readonly IEnvelope NoOp = new NoOp();
	}

	file class NoOp : IEnvelope {
		public void ReplyWith<U>(U message) where U : Message { }
	}
}
