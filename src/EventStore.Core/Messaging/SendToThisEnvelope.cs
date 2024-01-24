using EventStore.Core.Bus;

namespace EventStore.Core.Messaging {
	// USE ONLY WHEN YOU KNOW WHAT YOU ARE DOING
	public class SendToThisEnvelope : IEnvelope {
		private readonly object _receiver;

		public SendToThisEnvelope(object receiver) {
			_receiver = receiver;
		}

		public void ReplyWith<T>(T message) where T : class, Message {
			if (_receiver is IHandle<T> x)
				x.Handle(message);
		}
	}
}
