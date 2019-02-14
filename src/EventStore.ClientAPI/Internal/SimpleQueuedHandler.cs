using System;
using System.Collections.Generic;
using System.Threading;
using EventStore.ClientAPI.Common.Utils;
using System.Collections.Concurrent;

namespace EventStore.ClientAPI.Internal {
	internal class SimpleQueuedHandler {
		private readonly ILogger _log;
		private readonly ConcurrentQueue<Message> _messageQueue = new ConcurrentQueue<Message>();
		private readonly Dictionary<Type, Action<Message>> _handlers = new Dictionary<Type, Action<Message>>();
		private int _isProcessing;

		public SimpleQueuedHandler(ILogger log) {
			_log = log;
		}

		public void RegisterHandler<T>(Action<T> handler) where T : Message {
			Ensure.NotNull(handler, "handler");
			_handlers.Add(typeof(T), msg => handler((T)msg));
		}

		public void EnqueueMessage(Message message) {
			Ensure.NotNull(message, "message");

			_messageQueue.Enqueue(message);
			if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0)
				ThreadPool.QueueUserWorkItem(ProcessQueue);
		}

		private void ProcessQueue(object state) {
			do {
				while (_messageQueue.TryDequeue(out var message)) {
					if (!_handlers.TryGetValue(message.GetType(), out var handler)) {
						_log.Error($"No handler registered for type {message.GetType().FullName}");
					} else {
						try {
							handler(message);
						} catch (Exception e) {
							_log.Error(e, $"Error processing {message.GetType().FullName}");
						}
					}
				}

				Interlocked.Exchange(ref _isProcessing, 0);
			} while (!_messageQueue.IsEmpty && Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0);
		}
	}
}
