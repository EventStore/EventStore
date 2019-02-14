using System;
using EventStore.Core.Messaging;

namespace EventStore.Core.Helpers {
	public sealed class IODispatcherDelayedMessage : Message {
		private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

		public override int MsgTypeId {
			get { return TypeId; }
		}

		private readonly Guid _correlationId;
		private readonly Action _action;
		private readonly Guid? _messageCorrelationId;

		public IODispatcherDelayedMessage(Guid correlationId, Action action) {
			_action = action;
			_correlationId = correlationId;
		}

		public IODispatcherDelayedMessage(Guid correlationId, Action action, Guid? messageCorrelationId) {
			_action = action;
			_correlationId = correlationId;
			_messageCorrelationId = messageCorrelationId;
		}

		public Action Action {
			get { return _action; }
		}

		public Guid CorrelationId {
			get { return _correlationId; }
		}

		public Guid? MessageCorrelationId {
			get { return _messageCorrelationId; }
		}
	}
}
