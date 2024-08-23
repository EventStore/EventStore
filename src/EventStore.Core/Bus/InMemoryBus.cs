using System;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DotNext;
using DotNext.Diagnostics;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using ILogger = Serilog.ILogger;

namespace EventStore.Core.Bus;

/// <summary>
/// Synchronously dispatches messages to zero or more subscribers.
/// Subscribers are responsible for handling exceptions
/// </summary>
public partial class InMemoryBus : IBus, IHandle<Message> {
	public static InMemoryBus CreateTest(bool watchSlowMsg = true) =>
		new("Test", watchSlowMsg);

	public static readonly TimeSpan DefaultSlowMessageThreshold = TimeSpan.FromMilliseconds(48);
	private static readonly ILogger Log = Serilog.Log.ForContext<InMemoryBus>();

	private readonly FrozenDictionary<Type, MessageTypeHandler> _handlers;
	private readonly double _slowMsgThresholdMs;

	public InMemoryBus(string name, bool watchSlowMsg = true, TimeSpan? slowMsgThreshold = null) {
		_handlers = CreateMessageTypeHandlers();
		Name = name;

		if (watchSlowMsg)
			_slowMsgThresholdMs = slowMsgThreshold.GetValueOrDefault(DefaultSlowMessageThreshold).TotalMilliseconds;
	}

	public string Name { get; }

	public void Subscribe<T>(IHandle<T> handler) where T : Message {
		ArgumentNullException.ThrowIfNull(handler);

		if (!_handlers.TryGetValue(typeof(T), out var handlers))
			throw new GenericArgumentException<T>("Unexpected message type", nameof(handler));

		Debug.Assert(handlers is MessageTypeHandler<T>);
		Unsafe.As<MessageTypeHandler<T>>(handlers).AddHandler(handler);
	}

	public void Unsubscribe<T>(IHandle<T> handler) where T : Message {
		ArgumentNullException.ThrowIfNull(handler);

		if (!_handlers.TryGetValue(typeof(T), out var handlers))
			throw new GenericArgumentException<T>("Unexpected message type", nameof(handler));

		Debug.Assert(handlers is MessageTypeHandler<T>);
		Unsafe.As<MessageTypeHandler<T>>(handlers).RemoveHandler(handler);
	}

	public void Handle(Message message) => Publish(message);

	private bool IsSlowMsgWatchEnabled => BitConverter.DoubleToInt64Bits(_slowMsgThresholdMs) is not 0L;

	public void Publish(Message message) {
		if (!_handlers.TryGetValue(message.GetType(), out var handlers))
			throw new ArgumentOutOfRangeException(nameof(message), "Unexpected message type");

		// Perf: branching with single if-else statement is better than virtual dispatch
		if (IsSlowMsgWatchEnabled) {
			var ts = new Timestamp();

			handlers.Invoke(message);

			var elapsedMs = ts.ElapsedMilliseconds;
			if (elapsedMs > _slowMsgThresholdMs) {
				Log.Debug("SLOW BUS MSG [{bus}]: {message} - {elapsed}ms.",
					Name, message.GetType().Name, elapsedMs);
				if (elapsedMs > QueuedHandler.VerySlowMsgThreshold.TotalMilliseconds &&
				    message is not SystemMessage.SystemInit)
					Log.Error("---!!! VERY SLOW BUS MSG [{bus}]: {message} - {elapsed}ms.",
						Name, message.GetType().Name, elapsedMs);
			}
		} else {
			handlers.Invoke(message);
		}
	}
}
