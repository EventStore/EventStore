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

			_handlers[i] = new(output, defaultHandlers[i]);
			output.Clear(); // help GC
		}
	}

	public void Handle(Message message) => _handlers.Invoke(_stateRef.Value, message);

	[StructLayout(LayoutKind.Auto)]
	private readonly struct Handler(
		IReadOnlyDictionary<Type, Action<VNodeState, Message>> handlers,
		Action<VNodeState, Message> defaultHandler) {
		private readonly FrozenDictionary<Type, Action<VNodeState, Message>> _handlers = handlers.ToFrozenDictionary();
		private readonly Action<VNodeState, Message> _defaultHandler = defaultHandler ?? ThrowException;

		public void Invoke(VNodeState state, Message message) {
			scoped ref readonly var actionRef = ref _handlers.GetValueRefOrNullRef(message.GetType());

			Action<VNodeState, Message> action;
			if (Unsafe.IsNullRef(in actionRef) || (action = actionRef) is null)
				action = _defaultHandler;

			action.Invoke(state, message);
		}

		private static void ThrowException(VNodeState state, Message message) {
			throw new Exception($"Unhandled message: {message} occurred in state: {state}.");
		}
	}

	[InlineArray((int)VNodeState.MaxValue + 1)]
	[StructLayout(LayoutKind.Auto)]
	private struct HandlersBuffer {
		private Handler _handler;

		public readonly void Invoke(VNodeState index, Message message)
			=> Unsafe.Add(ref Unsafe.AsRef(in _handler), (int)index).Invoke(index, message);
	}
}
