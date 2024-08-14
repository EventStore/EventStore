using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using DotNext.Collections.Specialized;
using DotNext.Reflection;
using DotNext.Runtime;
using DotNext.Runtime.ExceptionServices;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.VNode;

public class VNodeFSM : IHandle<Message> {
	private readonly ReadOnlyValueReference<VNodeState> _stateRef;
	private readonly HandlersBuffer _handlers;
	private readonly DefaultHandlersBuffer _defaultHandlers;

	internal VNodeFSM(ReadOnlyValueReference<VNodeState> stateRef,
		IReadOnlyDictionary<Type, Action<VNodeState, Message>>[] handlers,
		Action<VNodeState, Message>[] defaultHandlers) {
		_stateRef = stateRef;

		for (var i = 0; i < handlers.Length; i++) {
			var collection = _handlers[i] = CreateMessageTypeHandlers();

			foreach (var (messageType, handler) in handlers[i]) {
				collection[messageType].Handler = handler;
			}
		}

		defaultHandlers.CopyTo(_defaultHandlers);
	}

	public void Handle(Message message) {
		var state = _stateRef.Value;

		if (_handlers[(int)state] is { } stateHandler
		    && stateHandler.TryGetValue(message.GetType(), out var handlers)
		    && handlers.Invoke(state, message)) {
			return;
		}

		if (_defaultHandlers[(int)state] is { } defaultHandler) {
			defaultHandler(state, message);
		} else {
			throw new Exception($"Unhandled message: {message} occurred in state: {state}.");
		}
	}

	private static FrozenDictionary<Type, MessageTypeHandler> CreateMessageTypeHandlers() {
		var handlers = new Dictionary<Type, MessageTypeHandler>(InMemoryBus.KnownMessageTypes.Count);

		foreach (var messageType in InMemoryBus.KnownMessageTypes) {
			var handler = new MessageTypeHandler();
			handlers.Add(messageType, handler);
		}

		foreach (var (messageType, handler) in handlers) {
			RegisterMessageType(handlers, messageType, handler);
		}

		// establish relationships between nodes
		return handlers.ToFrozenDictionary();

		static void RegisterMessageType(Dictionary<Type, MessageTypeHandler> messageTypes, Type messageType,
			MessageTypeHandler handler) {
			while (messageType.GetBaseTypes().FirstOrDefault(InMemoryBus.KnownMessageTypes.Contains) is { } baseType
			       && handler.Parent is null) {
				if (!messageTypes.TryGetValue(baseType, out var parent))
					Debug.Fail($"Unexpected message type {messageType}");

				handler.Parent = parent;
				handler = parent;
				messageType = baseType;
			}
		}
	}

	private sealed class MessageTypeHandler {
		public MessageTypeHandler Parent; // can be null
		public Action<VNodeState, Message> Handler;

		public bool Invoke(VNodeState state, Message message) {
			var result = Parent?.Invoke(state, message) ?? false;

			if (Handler is not null) {
				result = true;
				Handler.Invoke(state, message);
			}

			return result;
		}
	}

	[InlineArray((int)VNodeState.MaxValue + 1)]
	[StructLayout(LayoutKind.Auto)]
	private struct DefaultHandlersBuffer {
		private Action<VNodeState, Message> _handler;
	}

	[InlineArray((int)VNodeState.MaxValue + 1)]
	[StructLayout(LayoutKind.Auto)]
	private struct HandlersBuffer {
		private IReadOnlyDictionary<Type, MessageTypeHandler> _handler;
	}
}
