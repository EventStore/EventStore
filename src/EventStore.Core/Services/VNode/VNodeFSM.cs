using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
		ReadOnlySpan<IReadOnlyDictionary<Type, MulticastDelegate>> handlers,
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

			Debug.Assert(Enum.IsDefined((VNodeState)i));
			_handlers[i] = new((VNodeState)i, output, defaultHandlers[i]);
			output.Clear(); // help GC
		}
	}

	public void Handle(Message message) => _handlers.Invoke(_stateRef.Value, message);

	[StructLayout(LayoutKind.Auto)]
	private readonly struct Handler(
		VNodeState state,
		IReadOnlyDictionary<Type, HandlerAnalysisNode> handlers,
		Action<Message> defaultHandler) {
		private readonly FrozenDictionary<Type, MulticastDelegate> _handlers = handlers
			.Select(static pair => new KeyValuePair<Type, MulticastDelegate>(pair.Key, pair.Value.Handler))
			.ToFrozenDictionary();

		// Enum name is cached by the runtime, no allocation caused by Enum.GetName
		private readonly MulticastDelegate _defaultHandler = defaultHandler ?? Enum.GetName(state).ThrowException;

		public void Invoke(Message message) {
			scoped ref readonly var actionRef = ref _handlers.GetValueRefOrNullRef(message.GetType());

			if (Unsafe.IsNullRef(in actionRef)) {
				actionRef = ref _defaultHandler;
			}

			// We know that the actual handler type is Action<T> where T >= message.GetType()
			// Unsafe reinterpret case is valid due to ABI nature of reference types. Size
			// of reference is always 4 or 8 bytes regardless the actual type T.
			EnsureActionType(message.GetType(), actionRef);
			Unsafe.As<Action<Message>>(actionRef).Invoke(message);
		}

		[Conditional("DEBUG")]
		private static void EnsureActionType(Type expectedMessageType, [DisallowNull] MulticastDelegate handler) {
			var actualMessageType = handler.GetType().GetGenericArguments()[0];
			Debug.Assert(actualMessageType.IsAssignableFrom(expectedMessageType));
		}
	}

	[InlineArray((int)VNodeState.MaxValue + 1)]
	[StructLayout(LayoutKind.Auto)]
	private struct HandlersBuffer {
		private Handler _handler;

		public readonly void Invoke(VNodeState index, Message message)
			=> Unsafe.Add(ref Unsafe.AsRef(in _handler), (int)index).Invoke(message);
	}

	[StructLayout(LayoutKind.Auto)]
	private struct HandlerAnalysisNode {
		internal Type AnalyzedType;
		internal MulticastDelegate Handler;
	}
}

file static class DelegateHelpers {
	public static void ThrowException(this string stateName, Message message) {
		throw new Exception($"Unhandled message: {message} occurred in state: {stateName}.");
	}
}
