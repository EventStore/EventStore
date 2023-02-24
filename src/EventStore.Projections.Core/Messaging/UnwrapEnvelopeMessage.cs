using System;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Messaging {
	[DerivedMessage(ProjectionMessage.Misc)]
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
