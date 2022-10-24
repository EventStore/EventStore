using System;
using EventStore.Core.Messaging;

namespace EventStore.Projections.Core.Messaging {
	//qq any others to combine into this and relocate?
	[StatsGroup("projections-misc")]
	public enum MessageType {
		None = 0,
		UnwrapEnvelopeMessage = 1,
	}

	[StatsMessage(MessageType.UnwrapEnvelopeMessage)]
	public partial class UnwrapEnvelopeMessage : Message {
		private readonly Action _action;

		public UnwrapEnvelopeMessage(Action action) {
			_action = action;
		}

		public Action Action {
			get { return _action; }
		}
	}
}
