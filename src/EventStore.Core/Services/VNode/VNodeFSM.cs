using System;
using System.Collections.Generic;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.VNode {
	public class VNodeFSMUnoptimized : IHandle<Message> {
		private readonly Func<VNodeState> _getState;
		private readonly Dictionary<Type, Action<VNodeState, Message>>[] _handlers;
		private readonly Action<VNodeState, Message>[] _defaultHandlers;

		internal VNodeFSMUnoptimized(Func<VNodeState> getState,
			Dictionary<Type, Action<VNodeState, Message>>[] handlers,
			Action<VNodeState, Message>[] defaultHandlers) {
			_getState = getState;
			_handlers = handlers;
			_defaultHandlers = defaultHandlers;
		}

		public void Handle(Message message) {
			var state = _getState();
			var stateNum = (int)state;
			var handlers = _handlers[stateNum];

			var type = message.GetType();

			if (TryHandle(state, handlers, message, type))
				return;

			do {
				type = type.BaseType;
				if (TryHandle(state, handlers, message, type))
					return;
			} while (type != typeof(Message));

			if (_defaultHandlers[stateNum] != null) {
				_defaultHandlers[stateNum](state, message);
				return;
			}

			throw new InvalidOperationException(
				string.Format("Unhandled message: {0} occured in state: {1}.", message, state));
		}

		private bool TryHandle(VNodeState state,
			IDictionary<Type, Action<VNodeState, Message>> handlers,
			Message message,
			Type msgType) {
			if (handlers == null)
				return false;

			Action<VNodeState, Message> handler;
			if (!handlers.TryGetValue(msgType, out handler))
				return false;
			handler(state, message);
			return true;
		}
	}

	public class VNodeFSM : IHandle<Message> {
		private readonly Func<VNodeState> _getState;
		private readonly Action<VNodeState, Message>[][] _handlers;
		private readonly Action<VNodeState, Message>[] _defaultHandlers;

		internal VNodeFSM(Func<VNodeState> getState,
			Dictionary<Type, Action<VNodeState, Message>>[] handlers,
			Action<VNodeState, Message>[] defaultHandlers) {
			_getState = getState;

			_handlers = new Action<VNodeState, Message>[handlers.Length][];
			for (int i = 0; i < _handlers.Length; ++i) {
				_handlers[i] = new Action<VNodeState, Message>[MessageHierarchy.MaxMsgTypeId + 1];
				if (handlers[i] != null) {
					foreach (var handler in handlers[i]) {
						_handlers[i][MessageHierarchy.MsgTypeIdByType[handler.Key]] = handler.Value;
					}
				}
			}

			_defaultHandlers = defaultHandlers;
		}

		public void Handle(Message message) {
			var state = _getState();
			var stateNum = (int)state;
			var handlers = _handlers[stateNum];

			var parents = MessageHierarchy.ParentsByTypeId[message.MsgTypeId];
			for (int i = 0; i < parents.Length; ++i) {
				if (TryHandle(state, handlers, message, parents[i]))
					return;
			}

			if (_defaultHandlers[stateNum] != null) {
				_defaultHandlers[stateNum](state, message);
				return;
			}

			throw new Exception(string.Format("Unhandled message: {0} occurred in state: {1}.", message, state));
		}

		private static bool TryHandle(VNodeState state, Action<VNodeState, Message>[] handlers, Message message,
			int msgTypeId) {
			Action<VNodeState, Message> handler = handlers[msgTypeId];
			if (handler != null) {
				handler(state, message);
				return true;
			}

			return false;
		}
	}
}
