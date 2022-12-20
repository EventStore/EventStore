using System;
using EventStore.Core.Messaging;

namespace EventStore.Projections.Core.Messaging {
	[DerivedMessage]
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
