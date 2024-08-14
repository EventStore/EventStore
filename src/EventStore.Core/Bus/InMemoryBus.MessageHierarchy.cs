using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using DotNext.Reflection;
using EventStore.Core.Messaging;

namespace EventStore.Core.Bus;

public partial class InMemoryBus {

	/// <summary>
	/// Gets all discovered message types.
	/// </summary>
	public static IReadOnlySet<Type> KnownMessageTypes { get; }

	static InMemoryBus() {
		ReadOnlySpan<string> systemPrefixes = ["System.", "Microsoft."];
		var messageTypes = new HashSet<Type>(500);

		foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
			if (IsSystemAssembly(assembly, systemPrefixes))
				continue;

			foreach (Type messageType in LoadAvailableTypes(assembly).Where(IsMessageType)) {
				messageTypes.Add(messageType);
			}
		}

		KnownMessageTypes = messageTypes.ToFrozenSet();
		messageTypes.Clear(); // help GC

		static bool IsSystemAssembly(Assembly candidate, ReadOnlySpan<string> systemPrefixes) {
			if (candidate.IsDynamic)
				return true;

			ReadOnlySpan<char> fullName = candidate.FullName;
			foreach (ReadOnlySpan<char> prefix in systemPrefixes) {
				if (fullName.StartsWith(prefix))
					return true;
			}

			return false;
		}

		static bool IsMessageType(Type candidate)
			=> typeof(Message).IsAssignableFrom(candidate) && candidate.IsGenericTypeDefinition is false;
	}

	static Type[] LoadAvailableTypes(Assembly assembly) {
		try {
			return assembly.GetTypes();
		} catch (ReflectionTypeLoadException ex) {
			if (ex.LoaderExceptions.Length > 0)
				Log.Information("The exception(s) occured when scanning for message types: {e}",
					string.Join(",", ex.LoaderExceptions.Select(static x => x.Message)));
			else {
				Log.Information(ex, "Exception while scanning for message types");
			}

			return ex.Types;
		}
	}

	private static FrozenDictionary<Type, MessageTypeHandler> CreateMessageTypeHandlers() {
		var handlers = new Dictionary<Type, MessageTypeHandler>(KnownMessageTypes.Count);

		foreach (var messageType in KnownMessageTypes) {
			var handler =
				(MessageTypeHandler)Activator.CreateInstance(typeof(MessageTypeHandler<>).MakeGenericType(messageType));
			handlers.Add(messageType, handler);
		}

		foreach (var (messageType, handler) in handlers) {
			RegisterMessageType(handlers, messageType, handler);
		}

		// establish relationships between nodes
		return handlers.ToFrozenDictionary();

		static void RegisterMessageType(Dictionary<Type, MessageTypeHandler> messageTypes, Type messageType,
			MessageTypeHandler handler) {
			while (messageType.GetBaseTypes().FirstOrDefault(KnownMessageTypes.Contains) is { } baseType && handler.Parent is null) {
				if (!messageTypes.TryGetValue(baseType, out var parent))
					Debug.Fail($"Unexpected message type {messageType}");

				handler.Parent = parent;
				handler = parent;
				messageType = baseType;
			}
		}
	}

	private abstract class MessageTypeHandler {
		public abstract MessageTypeHandler Parent { get; set; }

		public abstract void Invoke(Message message);
	}

	private sealed class MessageTypeHandler<T> : MessageTypeHandler where T : Message {
		// Perf: invocation of handlers is hot path, it's better do devirt `Handle` method.
		// Devirtualized method is stored as a delegate
		private Action<T>[] _handlers = [];

		// Compat: assume that we have message types A > B with handlers handler(A) and handler(B).
		// Some parts of ESDB relies on the following behavior: if message B is published, the order of
		// handlers must be handler(A) -> handler(B) instead of handler(B) -> handler(A). That compat
		// issue prevents us from using tail call, because we need to call parent handlers first.
		public override MessageTypeHandler Parent {
			get => _handlers is [{ Target: MessageTypeHandler handler }, ..] ? handler : null;
			set => _handlers = value is null ? [] : [value.Invoke];
		}

		public override void Invoke(Message message) {
			Debug.Assert(message is T);

			foreach (var handler in Volatile.Read(in _handlers)) {
				handler.Invoke(Unsafe.As<T>(message));
			}
		}

		internal void AddHandler(IHandle<T> handler) {
			Debug.Assert(handler is not null);

			Action<T> devirtHandler = handler.Handle;
			for (Action<T>[] newArray;; Array.Clear(newArray)) {
				var currentArray = _handlers;

				// Perf: array is preferred over ImmutableHashSet because enumeration speed is much more important
				// (for Publish method) than perf of subscription methods.
				if (IndexOf(currentArray, handler) >= 0)
					break;

				newArray = new Action<T>[currentArray.Length + 1];
				Array.Copy(currentArray, newArray, currentArray.Length);
				newArray[currentArray.Length] = devirtHandler;

				if (Interlocked.CompareExchange(ref _handlers, newArray, currentArray) == currentArray)
					break;
			}
		}

		private static int IndexOf(Action<T>[] handlers, IHandle<T> handler) {
			for (var i = 0; i < handlers.Length; i++) {
				if (ReferenceEquals(handlers[i].Target, handler))
					return i;
			}

			return -1;
		}

		internal void RemoveHandler(IHandle<T> handler) {
			Debug.Assert(handler is not null);

			for (var currentArray = _handlers;;) {
				var index = IndexOf(currentArray, handler);
				if (index < 0 || currentArray.Length is 0)
					break;

				// fast path loop - no need to search over array
				for (Action<T>[] newArray;; Array.Clear(newArray)) {

					if (currentArray.Length > 1) {
						newArray = new Action<T>[currentArray.Length - 1];
						Array.Copy(currentArray, newArray, index);
						Array.Copy(currentArray, index + 1, newArray, index, currentArray.Length - index - 1);
					} else {
						newArray = [];
					}

					if (Interlocked.CompareExchange(ref _handlers, newArray, currentArray) == currentArray)
						return;

					currentArray = _handlers;
					if (currentArray.Length >= index || !ReferenceEquals(currentArray[index], handler))
						break;
				}
			}
		}
	}
}
