using System;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.VNode;

public readonly ref struct VNodeFSMHandling<TMessage>
	where TMessage : Message {
	private readonly VNodeFSMStatesDefinition _stateDef;
	private readonly bool _defaultHandler;

	internal VNodeFSMHandling(VNodeFSMStatesDefinition stateDef, bool defaultHandler) {
		_stateDef = stateDef;
		_defaultHandler = defaultHandler;
	}

	public VNodeFSMStatesDefinition Do(Action<TMessage> handler) {
		if (_defaultHandler) {
			foreach (var state in _stateDef.States) {
				_stateDef.FSM.AddDefaultHandler(state, handler.Invoke);
			}
		} else {
			foreach (var state in _stateDef.States) {
				_stateDef.FSM.AddHandler<TMessage>(state, handler.Invoke);
			}
		}

		return _stateDef;
	}

	public VNodeFSMStatesDefinition Ignore() {
		if (_defaultHandler) {
			foreach (var state in _stateDef.States) {
				_stateDef.FSM.AddDefaultHandler(state, NoOp);
			}
		} else {
			foreach (var state in _stateDef.States) {
				_stateDef.FSM.AddHandler<TMessage>(state, NoOp);
			}
		}

		return _stateDef;

		static void NoOp(VNodeState state, Message msg) {
		}
	}

	public VNodeFSMStatesDefinition ForwardTo(IPublisher publisher) {
		Ensure.NotNull(publisher, "publisher");
		return Do(publisher.Publish);
	}
}

file static class DelegateHelpers {
	public static void Invoke<TMessage>(this Action<TMessage> action, VNodeState state, Message message)
		where TMessage : Message
		=> action.Invoke((TMessage)message);
}
