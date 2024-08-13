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
public partial class InMemoryBus : IBus, ISubscriber, IPublisher, IHandle<Message> {
	public static InMemoryBus CreateTest(bool watchSlowMsg = true) => new("Test", watchSlowMsg);

	public static readonly TimeSpan DefaultSlowMessageThreshold = TimeSpan.FromMilliseconds(48);
	private static readonly ILogger Log = Serilog.Log.ForContext<InMemoryBus>();

	public string Name { get; }

	private readonly FrozenDictionary<Type, MessageTypeHandler> _handlers;
	private readonly Action<MessageTypeHandler, Message> _invoker;

	public InMemoryBus(string name, bool watchSlowMsg = true, TimeSpan? slowMsgThreshold = null) {
		Name = name;

		_handlers = CreateMessageTypeHandlers();
		_invoker = watchSlowMsg
			? CreateInvokerWithWatcher(name, (slowMsgThreshold ?? DefaultSlowMessageThreshold).TotalMilliseconds)
			: new Action<Message>(new MessageTypeHandler<Message>().Invoke).Method
				.CreateDelegate<Action<MessageTypeHandler, Message>>();

		static Action<MessageTypeHandler, Message> CreateInvokerWithWatcher(string name, double slowMsgThresholdMs) {
			return (handler, message) => {
				var ts = new Timestamp();

				handler.Invoke(message);

				var elapsedMs = ts.ElapsedMilliseconds;
				if (elapsedMs > slowMsgThresholdMs) {
					Log.Debug("SLOW BUS MSG [{bus}]: {message} - {elapsed}ms. Handler: {handler}.",
						name, message.GetType().Name, elapsedMs, message.GetType().Name);
					if (elapsedMs > QueuedHandler.VerySlowMsgThreshold.TotalMilliseconds &&
					    message is not SystemMessage.SystemInit)
						Log.Error("---!!! VERY SLOW BUS MSG [{bus}]: {message} - {elapsed}ms. Handler: {handler}.",
							name, message.GetType().Name, elapsedMs, message.GetType().Name);
				}
			};
		}
	}

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

	public void Handle(Message message) {
		Publish(message);
	}

	public void Publish(Message message) {
		if (!_handlers.TryGetValue(message.GetType(), out var handlers))
			throw new ArgumentOutOfRangeException(nameof(message), "Unexpected message type");

		_invoker(handlers, message);
	}
}
