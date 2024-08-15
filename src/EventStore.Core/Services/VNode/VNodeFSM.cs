using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotNext.Runtime;
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
		Debug.Assert(handlers.Length == (int)VNodeState.MaxValue + 1);
		Debug.Assert(defaultHandlers.Length == (int)VNodeState.MaxValue + 1);

		_stateRef = stateRef;

		var output = new Dictionary<Type, Action<VNodeState, Message>>();
		for (var i = 0; i < handlers.Length; i++) {
			var input = handlers[i];

			foreach (var knownMessageType in InMemoryBus.KnownMessageTypes) {
				foreach (var (messageType, action) in input) {
					if (messageType.IsAssignableFrom(knownMessageType)) {
						ref var handle =
							ref CollectionsMarshal.GetValueRefOrAddDefault(output, knownMessageType,
								out _);
						handle += action;
					}
				}
			}

			_handlers[i] = output.ToFrozenDictionary();
			output.Clear(); // help GC
		}

		defaultHandlers.CopyTo(_defaultHandlers);
	}

	public void Handle(Message message) {
		var state = _stateRef.Value;

		if (_handlers[state, message.GetType()] is { } handlers) {
			handlers.Invoke(state, message);
		} else if (_defaultHandlers[state] is { } defaultHandler) {
			defaultHandler(state, message);
		} else {
			throw new Exception($"Unhandled message: {message} occurred in state: {state}.");
		}
	}

	[InlineArray((int)VNodeState.MaxValue + 1)]
	[StructLayout(LayoutKind.Auto)]
	private struct DefaultHandlersBuffer {
		private Action<VNodeState, Message> _handler;

		public readonly Action<VNodeState, Message> this[VNodeState index]
			=> Unsafe.Add(ref Unsafe.AsRef(in _handler), (int)index);
	}

	[InlineArray((int)VNodeState.MaxValue + 1)]
	[StructLayout(LayoutKind.Auto)]
	private struct HandlersBuffer {
		private FrozenDictionary<Type, Action<VNodeState, Message>> _handler;

		public readonly Action<VNodeState, Message> this[VNodeState index, Type messageType] {
			get {
				var dictionary = Unsafe.Add(ref Unsafe.AsRef(in _handler), (int)index);
				dictionary.TryGetValue(messageType, out var action);
				return action;
			}
		}
	}
}
