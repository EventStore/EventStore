using System;
using EventStore.Core.Messaging;

namespace EventStore.Projections.Core.Messaging {
	public class UnwrapEnvelopeMessage : Message {
		private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

		public override int MsgTypeId {
			get { return TypeId; }
		}

		private readonly Action _action;

		public UnwrapEnvelopeMessage(Action action) {
			_action = action;
		}

		public Action Action {
			get { return _action; }
		}
	}
}
