using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
		ReadOnlySpan<IReadOnlyDictionary<Type, Action<Message>>> handlers,
		ReadOnlySpan<Action<Message>> defaultHandlers) {
		Debug.Assert(handlers.Length == (int)VNodeState.MaxValue + 1);
		Debug.Assert(defaultHandlers.Length == (int)VNodeState.MaxValue + 1);

		_stateRef = stateRef;

		var output = new Dictionary<Type, HandlerAnalysisNode>();
		for (var i = 0; i < handlers.Length; i++) {
			if (handlers[i] is { } input) {
				// register each handler in input against _all_ types it can handle
				foreach (var knownMessageType in InMemoryBus.KnownMessageTypes) {
					foreach (var (messageType, action) in input) {
						Debug.Assert(action is not null);
						if (messageType.IsAssignableFrom(knownMessageType)) {
							ref var node =
								ref CollectionsMarshal.GetValueRefOrAddDefault(output, knownMessageType,
									out _);

							// if two handlers can handle the same message at different levels
							// of the message class hierarchy, only call the most derived one
							if (node.AnalyzedType?.IsAssignableFrom(messageType) ?? true) {
								node.AnalyzedType = messageType;
								node.Handler = action;
							}
						}
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
		IReadOnlyDictionary<Type, HandlerAnalysisNode> handlers,
		Action<Message> defaultHandler) {
		private readonly FrozenDictionary<Type, Action<Message>> _handlers = handlers
			.Select(static pair => new KeyValuePair<Type, Action<Message>>(pair.Key, pair.Value.Handler))
			.ToFrozenDictionary();

		private readonly Action<VNodeState, Message> _defaultHandler =
			defaultHandler is not null ? defaultHandler.InvokeWithoutState : ThrowException;

		public void Invoke(VNodeState state, Message message) {
			scoped ref readonly var actionRef = ref _handlers.GetValueRefOrNullRef(message.GetType());

			if (Unsafe.IsNullRef(in actionRef)) {
				_defaultHandler(state, message);
				return;
			}

			actionRef.Invoke(message);
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

	[StructLayout(LayoutKind.Auto)]
	private struct HandlerAnalysisNode {
		internal Type AnalyzedType;
		internal Action<Message> Handler;
	}
}

file static class DelegateHelpers {
	public static void InvokeWithoutState<TMessage>(this Action<TMessage> action, VNodeState state, Message message)
		where TMessage : Message
		=> action.Invoke((TMessage)message);
}
