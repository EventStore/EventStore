using System;
using EventStore.Core.Messaging;
using JetBrains.Annotations;

namespace EventStore.Core.Bus {
	internal interface IMessageHandler {
		string HandlerName { get; }
		bool TryHandle(Message message);
		bool IsSame<T>(object handler);
	}

	internal class MessageHandler<T> : IMessageHandler where T : Message {
		public string HandlerName { get; private set; }

		private readonly IHandle<T> _handler;

		public MessageHandler([NotNull] IHandle<T> handler, string handlerName) {
			_handler = handler ?? throw new ArgumentNullException(nameof(handler));
			HandlerName = handlerName ?? string.Empty;
		}

		public bool TryHandle(Message message) {
			if (message is not T typedMessage) return false;
			_handler.Handle(typedMessage);
			return true;
		}

		public bool IsSame<T2>(object handler) {
			return ReferenceEquals(_handler, handler) && typeof(T) == typeof(T2);
		}

		public override string ToString() {
			return string.IsNullOrEmpty(HandlerName) ? _handler.ToString() : HandlerName;
		}
	}
}
