using EventStore.Core.Messaging;

namespace EventStore.Core.Bus {
	public class WideningHandler<TInput, TOutput> : IHandle<TInput>
		where TInput : TOutput
		where TOutput : Message {
		private readonly IHandle<TOutput> _handler;

		public WideningHandler(IHandle<TOutput> handler) {
			_handler = handler;
		}

		public void Handle(TInput message) {
			_handler.Handle(message);
		}

		public override string ToString() {
			return string.Format("WideningHandler<{0}, {1}>({2})", typeof(TInput).Name, typeof(TOutput).Name, _handler);
		}
	}
}
