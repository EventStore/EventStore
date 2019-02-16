using System;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.VNode {
	public class VNodeFSMHandling<TMessage> where TMessage : Message {
		private readonly VNodeFSMStatesDefinition _stateDef;
		private readonly bool _defaultHandler;

		public VNodeFSMHandling(VNodeFSMStatesDefinition stateDef, bool defaultHandler = false) {
			_stateDef = stateDef;
			_defaultHandler = defaultHandler;
		}

		public VNodeFSMStatesDefinition Do(Action<VNodeState, TMessage> handler) {
			foreach (var state in _stateDef.States) {
				if (_defaultHandler)
					_stateDef.FSM.AddDefaultHandler(state, (s, m) => handler(s, (TMessage)m));
				else
					_stateDef.FSM.AddHandler<TMessage>(state, (s, m) => handler(s, (TMessage)m));
			}

			return _stateDef;
		}

		public VNodeFSMStatesDefinition Do(Action<TMessage> handler) {
			foreach (var state in _stateDef.States) {
				if (_defaultHandler)
					_stateDef.FSM.AddDefaultHandler(state, (s, m) => handler((TMessage)m));
				else
					_stateDef.FSM.AddHandler<TMessage>(state, (s, m) => handler((TMessage)m));
			}

			return _stateDef;
		}

		public VNodeFSMStatesDefinition Ignore() {
			foreach (var state in _stateDef.States) {
				if (_defaultHandler)
					_stateDef.FSM.AddDefaultHandler(state, (s, m) => { });
				else
					_stateDef.FSM.AddHandler<TMessage>(state, (s, m) => { });
			}

			return _stateDef;
		}

		public VNodeFSMStatesDefinition Throw() {
			foreach (var state in _stateDef.States) {
				if (_defaultHandler)
					_stateDef.FSM.AddDefaultHandler(state, (s, m) => { throw new NotSupportedException(); });
				else
					_stateDef.FSM.AddHandler<TMessage>(state, (s, m) => { throw new NotSupportedException(); });
			}

			return _stateDef;
		}

		public VNodeFSMStatesDefinition ForwardTo(IPublisher publisher) {
			Ensure.NotNull(publisher, "publisher");
			return Do(publisher.Publish);
		}
	}
}
