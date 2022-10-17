using System;
using EventStore.Core.Messaging;

namespace EventStore.Core.Helpers {
	public sealed class IODispatcherDelayedMessage : Message {
		private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

		public override int MsgTypeId {
			get { return TypeId; }
		}

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
