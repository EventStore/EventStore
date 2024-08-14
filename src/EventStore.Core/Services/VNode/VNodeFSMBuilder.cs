using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using DotNext.Runtime;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.VNode {
	/// <summary>
	/// Builder syntax for constructing <see cref="VNodeFSM"/> in the code
	/// </summary>
	public class VNodeFSMBuilder {
		private readonly ReadOnlyValueReference<VNodeState> _stateRef;
		private readonly Dictionary<Type, Action<VNodeState, Message>>[] _handlers;
		private readonly Action<VNodeState, Message>[] _defaultHandlers;

		public VNodeFSMBuilder(ReadOnlyValueReference<VNodeState> stateRef) {
			_stateRef = stateRef;

			var maxState = (int)Enum.GetValues<VNodeState>().Max();
			_handlers = new Dictionary<Type, Action<VNodeState, Message>>[maxState + 1];
			_defaultHandlers = new Action<VNodeState, Message>[maxState + 1];
		}

		public VNodeFSMBuilder(VNodeState state)
			: this(Constant(state)) {
		}

		private static ReadOnlyValueReference<VNodeState> Constant(VNodeState state) {
			var box = new StrongBox<VNodeState> { Value = state };
			return new(box, ref box.Value);
		}

		internal void AddHandler<TActualMessage>(VNodeState state, Action<VNodeState, Message> handler)
			where TActualMessage : Message {
			var stateNum = (int)state;

			Dictionary<Type, Action<VNodeState, Message>> stateHandlers = _handlers[stateNum];
			if (stateHandlers == null)
				stateHandlers = _handlers[stateNum] = new Dictionary<Type, Action<VNodeState, Message>>();

			//var existingHandler = stateHandlers[typeof (TActualMessage)];
			//stateHandlers[typeof (TActualMessage)] = existingHandler == null
			//                                            ? handler
			//                                            : (s, m) => { existingHandler(s, m); handler(s, m); };

			if (stateHandlers.ContainsKey(typeof(TActualMessage)))
				throw new InvalidOperationException(
					string.Format("Handler already defined for state {0} and message {1}",
						state,
						typeof(TActualMessage).FullName));
			stateHandlers[typeof(TActualMessage)] = handler;
		}

		internal void AddDefaultHandler(VNodeState state, Action<VNodeState, Message> handler) {
			var stateNum = (int)state;
			//var existingHandler = _defaultHandlers[stateNum];
			//_defaultHandlers[stateNum] = existingHandler == null
			//                                ? handler
			//                                : (s, m) => { existingHandler(s, m); handler(s, m); };
			if (_defaultHandlers[stateNum] != null)
				throw new InvalidOperationException(string.Format("Default handler already defined for state {0}",
					state));
			_defaultHandlers[stateNum] = handler;
		}

		public VNodeFSMStatesDefinition InAnyState() {
			var allStates = Enum.GetValues<VNodeState>().Distinct().ToArray();
			return new VNodeFSMStatesDefinition(this, allStates);
		}

		public VNodeFSMStatesDefinition InState(VNodeState state) {
			return new VNodeFSMStatesDefinition(this, state);
		}

		public VNodeFSMStatesDefinition InStates(params VNodeState[] states) {
			return new VNodeFSMStatesDefinition(this, states);
		}

		public VNodeFSMStatesDefinition InAllStatesExcept(VNodeState[] states) {
			Ensure.Positive(states.Length, "states.Length");

			var s = Enum.GetValues<VNodeState>().Except(states).Distinct().ToArray();
			return new VNodeFSMStatesDefinition(this, s);
		}

		public VNodeFSM Build() {
			return new VNodeFSM(_stateRef, _handlers, _defaultHandlers);
		}
	}
}
