using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI.Internal
{
    internal class SimpleQueuedHandler
    {
        private readonly ConcurrentQueue<Message> _messageQueue = new ConcurrentQueue<Message>();
        private readonly Dictionary<Type, Action<Message>> _handlers = new Dictionary<Type, Action<Message>>();
        private int _isProcessing;

        public void RegisterHandler<T>(Action<T> handler) where T : Message
        {
            Ensure.NotNull(handler, "handler");
            _handlers.Add(typeof(T), msg => handler((T)msg));
        }

        public void EnqueueMessage(Message message)
        {
            Ensure.NotNull(message, "message");

            _messageQueue.Enqueue(message);
            if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0)
                ThreadPool.QueueUserWorkItem(ProcessQueue);
        }

        private void ProcessQueue(object state)
        {
            do
            {
                Message message;

                while (_messageQueue.TryDequeue(out message))
                {
                    Action<Message> handler;
                    if (!_handlers.TryGetValue(message.GetType(), out handler))
                        throw new Exception(string.Format("No handler registered for message {0}", message.GetType().Name));
                    handler(message);
                }

                Interlocked.Exchange(ref _isProcessing, 0);
            } while (_messageQueue.Count > 0 && Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0);
        }
    }
}