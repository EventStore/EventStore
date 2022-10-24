using System;
using EventStore.Core.Messaging;

namespace EventStore.Core.Helpers {
	//qq there might be other messages that ought to go in the misc group
	// and this should go somewhere else
	[StatsGroup("misc")]
	public enum MessageType {
		None = 0,
		IODispatcherDelayedMessage = 1,
	}

	[StatsMessage(MessageType.IODispatcherDelayedMessage)]
	public sealed partial class IODispatcherDelayedMessage : Message {
		private readonly Guid _correlationId;
		private readonly ICorrelatedTimeout _timeout;
		private readonly Guid? _messageCorrelationId;

		public IODispatcherDelayedMessage(Guid correlationId, ICorrelatedTimeout timeout) {
			_timeout = timeout;
			_correlationId = correlationId;
		}

		public IODispatcherDelayedMessage(Guid correlationId, ICorrelatedTimeout timeout, Guid messageCorrelationId) {
			_timeout = timeout;
			_correlationId = correlationId;
			_messageCorrelationId = messageCorrelationId;
		}

		public void Timeout() {
			_timeout.Timeout(_messageCorrelationId ?? Guid.Empty);
		}

		public Guid CorrelationId {
			get { return _correlationId; }
		}

		public Guid? MessageCorrelationId {
			get { return _messageCorrelationId; }
		}
	}
}
