using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.VNode {
	public class VNodeFSMStatesDefinition {
		internal readonly VNodeFSMBuilder FSM;
		internal readonly VNodeState[] States;

		public VNodeFSMStatesDefinition(VNodeFSMBuilder fsm, params VNodeState[] states) {
			FSM = fsm;
			States = states;
		}

		public VNodeFSMHandling<TMessage> When<TMessage>() where TMessage : Message {
			return new VNodeFSMHandling<TMessage>(this);
		}

		public VNodeFSMHandling<Message> WhenOther() {
			return new VNodeFSMHandling<Message>(this, defaultHandler: true);
		}

		public VNodeFSMStatesDefinition InAnyState() {
			return FSM.InAnyState();
		}

		public VNodeFSMStatesDefinition InState(VNodeState state) {
			return FSM.InState(state);
		}

		public VNodeFSMStatesDefinition InStates(params VNodeState[] states) {
			return FSM.InStates(states);
		}

		public VNodeFSMStatesDefinition InAllStatesExcept(params VNodeState[] states) {
			Ensure.Positive(states.Length, "states.Length");
			return FSM.InAllStatesExcept(states);
		}

		public VNodeFSM Build() {
			return FSM.Build();
		}
	}
}
