using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using DotNext.Reflection;
using DotNext.Runtime.ExceptionServices;
using EventStore.Core.Messaging;

namespace EventStore.Core.Bus;

public partial class InMemoryBus {
	private static readonly FrozenSet<Type> MessageTypes;

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

		MessageTypes = messageTypes.ToFrozenSet();
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
		var handlers = new Dictionary<Type, MessageTypeHandler>(MessageTypes.Count);

		foreach (var messageType in MessageTypes) {
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
			while (messageType.GetBaseTypes().FirstOrDefault(MessageTypes.Contains) is { } baseType && handler.Parent is null) {
				if (!messageTypes.TryGetValue(baseType, out var parent))
					Debug.Fail($"Unexpected message type {messageType}");

				handler.Parent = parent;
				handler = parent;
				messageType = baseType;
			}
		}
	}

	private abstract class MessageTypeHandler {
		public MessageTypeHandler Parent; // can be null

		public abstract void Invoke(Message message, ref ExceptionAggregator exceptions);

		public void Invoke(Message message) {
			var exceptions = new ExceptionAggregator();
			Invoke(message, ref exceptions);
			exceptions.ThrowIfNeeded();
		}
	}

	private sealed class MessageTypeHandler<T> : MessageTypeHandler where T : Message {
		private IHandle<T>[] _handlers = [];

		public override void Invoke(Message message, ref ExceptionAggregator exceptions) {
			Debug.Assert(message is T);

			foreach (var handler in Volatile.Read(in _handlers)) {
				try {
					handler.Handle(Unsafe.As<T>(message));
				} catch (Exception e) {
					exceptions.Add(e);
				}
			}

			Parent?.Invoke(message, ref exceptions);
		}

		internal void AddHandler(IHandle<T> handler) {
			Debug.Assert(handler is not null);

			for (IHandle<T>[] newArray;; Array.Clear(newArray)) {
				var currentArray = _handlers;
				newArray = new IHandle<T>[currentArray.Length + 1];
				Array.Copy(currentArray, newArray, currentArray.Length);
				newArray[currentArray.Length] = handler;

				if (Interlocked.CompareExchange(ref _handlers, newArray, currentArray) == currentArray)
					break;
			}
		}

		internal void RemoveHandler(IHandle<T> handler) {
			Debug.Assert(handler is not null);

			for (var currentArray = _handlers;;) {
				var index = Array.IndexOf(currentArray, handler);
				if (index < 0 || currentArray.Length is 0)
					break;

				// fast path loop - no need to search over array
				for (IHandle<T>[] newArray;; Array.Clear(newArray)) {

					if (currentArray.Length > 1) {
						newArray = new IHandle<T>[currentArray.Length - 1];
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
