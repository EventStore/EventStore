// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 
using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Messaging;

namespace EventStore.Core.Bus
{
    /// <summary>
    /// Synchronously dispatches messages to zero or more subscribers.
    /// Subscribers are responsible for handling exceptions
    /// </summary>
    public class InMemoryBusUnoptimized : IBus, ISubscriber, IPublisher, IHandle<Message>
    {
        public static InMemoryBusUnoptimized CreateTest()
        {
            return new InMemoryBusUnoptimized();
        }

        public static readonly TimeSpan DefaultSlowMessageThreshold = TimeSpan.FromMilliseconds(48);

        private static readonly ILogger Log = LogManager.GetLoggerFor<InMemoryBus>();

        public string Name { get; private set; }

        private readonly Dictionary<Type, List<IMessageHandler>> _typeHash;

        private readonly bool _watchSlowMsg;
        private readonly TimeSpan _slowMsgThreshold;

        private InMemoryBusUnoptimized(): this("Test")
        {
        }

        public InMemoryBusUnoptimized(string name, bool watchSlowMsg = true, TimeSpan? slowMsgThreshold = null)
        {
            _typeHash = new Dictionary<Type, List<IMessageHandler>>();

            Name = name;
            _watchSlowMsg = watchSlowMsg;
            _slowMsgThreshold = slowMsgThreshold ?? DefaultSlowMessageThreshold;
        }

        public void Subscribe<T>(IHandle<T> handler) where T : Message
        {
            Ensure.NotNull(handler, "handler");

            List<IMessageHandler> handlers;
            if (!_typeHash.TryGetValue(typeof(T), out handlers))
            {
                handlers = new List<IMessageHandler>();
                _typeHash.Add(typeof(T), handlers);
            }
            if (!handlers.Any(x => x.IsSame<T>(handler)))
                handlers.Add(new MessageHandler<T>(handler, handler.GetType().Name));
        }

        public void Unsubscribe<T>(IHandle<T> handler) where T : Message
        {
            Ensure.NotNull(handler, "handler");

            List<IMessageHandler> handlers;
            if (_typeHash.TryGetValue(typeof(T), out handlers))
            {
                var messageHandler = handlers.FirstOrDefault(x => x.IsSame<T>(handler));
                if (messageHandler != null)
                    handlers.Remove(messageHandler);
            }
        }

        public void Handle(Message message)
        {
            Publish(message);
        }

        public void Publish(Message message)
        {
            Ensure.NotNull(message,"message");
            DispatchByType(message);
        }

        private void DispatchByType(Message message)
        {
            var type = message.GetType();
            PublishByType(message, type);
            do
            {
                type = type.BaseType;
                PublishByType(message, type);
            } while (type != typeof(Message));
        }

        private void PublishByType(Message message, Type type)
        {
            List<IMessageHandler> handlers;
            if (_typeHash.TryGetValue(type, out handlers))
            {
                for (int i = 0, n = handlers.Count; i < n; ++i)
                {
                    var handler = handlers[i];
                    if (_watchSlowMsg)
                    {
                        var start = DateTime.UtcNow;

                        handler.TryHandle(message);

                        var elapsed = DateTime.UtcNow - start;
                        if (elapsed > _slowMsgThreshold)
                            Log.Trace("SLOW BUS MSG [{0}]: {1} - {2}ms. Handler: {3}.", Name, message.GetType().Name, (int)elapsed.TotalMilliseconds, handler.HandlerName);
                    }
                    else
                    {
                        handler.TryHandle(message);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Synchronously dispatches messages to zero or more subscribers.
    /// Subscribers are responsible for handling exceptions
    /// </summary>
    public class InMemoryBus2 : IBus, ISubscriber, IPublisher, IHandle<Message>
    {
        public static InMemoryBus2 CreateTest()
        {
            return new InMemoryBus2();
        }

        public static readonly TimeSpan DefaultSlowMessageThreshold = TimeSpan.FromMilliseconds(48);

        private static readonly ILogger Log = LogManager.GetLoggerFor<InMemoryBus2>();

        public string Name { get; private set; }

        private readonly Dictionary<Type, List<IMessageHandler>> _typeHash;

        private readonly bool _watchSlowMsg;
        private readonly TimeSpan _slowMsgThreshold;

        private InMemoryBus2(): this("Test")
        {
        }

        public InMemoryBus2(string name, bool watchSlowMsg = true, TimeSpan? slowMsgThreshold = null)
        {
            _typeHash = new Dictionary<Type, List<IMessageHandler>>();

            Name = name;
            _watchSlowMsg = watchSlowMsg;
            _slowMsgThreshold = slowMsgThreshold ?? DefaultSlowMessageThreshold;
        }

        public void Subscribe<T>(IHandle<T> handler) where T : Message
        {
            Ensure.NotNull(handler, "handler");

            List<Type> descendants;
            if (!MessageHierarchy.Descendants.TryGetValue(typeof(T), out descendants))
                throw new Exception(string.Format("No descendants for message of type '{0}'.", typeof(T).Name));

            foreach (var descendant in descendants)
            {
                List<IMessageHandler> handlers;
                if (!_typeHash.TryGetValue(descendant, out handlers))
                {
                    handlers = new List<IMessageHandler>();
                    _typeHash.Add(descendant, handlers);
                }
                if (!handlers.Any(x => x.IsSame<T>(handler)))
                    handlers.Add(new MessageHandler<T>(handler, handler.GetType().Name));
            }

        }

        public void Unsubscribe<T>(IHandle<T> handler) where T : Message
        {
            Ensure.NotNull(handler, "handler");

            List<Type> descendants;
            if (!MessageHierarchy.Descendants.TryGetValue(typeof(T), out descendants))
                throw new Exception(string.Format("No descendants for message of type '{0}'.", typeof(T).Name));

            foreach (var descendant in descendants)
            {
                List<IMessageHandler> handlers;
                if (_typeHash.TryGetValue(descendant, out handlers))
                {
                    var messageHandler = handlers.FirstOrDefault(x => x.IsSame<T>(handler));
                    if (messageHandler != null)
                        handlers.Remove(messageHandler);
                }
            }
        }

        public void Handle(Message message)
        {
            Publish(message);
        }

        public void Publish(Message message)
        {
            Ensure.NotNull(message, "message");
            PublishByType(message, message.GetType());
        }

        private void PublishByType(Message message, Type type)
        {
            List<IMessageHandler> handlers;
            if (!_typeHash.TryGetValue(type, out handlers)) 
                return;

            for (int i = 0, n = handlers.Count; i < n; ++i)
            {
                var handler = handlers[i];
                if (_watchSlowMsg)
                {
                    var start = DateTime.UtcNow;

                    handler.TryHandle(message);

                    var elapsed = DateTime.UtcNow - start;
                    if (elapsed > _slowMsgThreshold)
                    {
                        Log.Trace("SLOW BUS MSG [{0}]: {1} - {2}ms. Handler: {3}.",
                                  Name, message.GetType().Name, (int) elapsed.TotalMilliseconds, handler.HandlerName);
                    }
                }
                else
                {
                    handler.TryHandle(message);
                }
            }
        }
    }

    /// <summary>
    /// Synchronously dispatches messages to zero or more subscribers.
    /// Subscribers are responsible for handling exceptions
    /// </summary>
    public class InMemoryBus : IBus, ISubscriber, IPublisher, IHandle<Message>
    {
        public static InMemoryBus CreateTest()
        {
            return new InMemoryBus();
        }

        public static readonly TimeSpan DefaultSlowMessageThreshold = TimeSpan.FromMilliseconds(48);
        private static readonly ILogger Log = LogManager.GetLoggerFor<InMemoryBus>();

        public string Name { get; private set; }

        private readonly List<IMessageHandler>[] _handlers;

        private readonly bool _watchSlowMsg;
        private readonly TimeSpan _slowMsgThreshold;

        private InMemoryBus(): this("Test"){
        }

        public InMemoryBus(string name, bool watchSlowMsg = true, TimeSpan? slowMsgThreshold = null)
        {
            Name = name;
            _watchSlowMsg = watchSlowMsg;
            _slowMsgThreshold = slowMsgThreshold ?? DefaultSlowMessageThreshold;

            _handlers = new List<IMessageHandler>[MessageHierarchy.MaxMsgTypeId + 1];
            for (int i = 0; i < _handlers.Length; ++i)
            {
                _handlers[i] = new List<IMessageHandler>();
            }
        }

        public void Subscribe<T>(IHandle<T> handler) where T : Message
        {
            Ensure.NotNull(handler, "handler");

            int[] descendants = MessageHierarchy.DescendantsByType[typeof (T)];
            for (int i = 0; i < descendants.Length; ++i)
            {
                var handlers = _handlers[descendants[i]];
                if (!handlers.Any(x => x.IsSame<T>(handler)))
                    handlers.Add(new MessageHandler<T>(handler, handler.GetType().Name));
            }
        }

        public void Unsubscribe<T>(IHandle<T> handler) where T : Message
        {
            Ensure.NotNull(handler, "handler");

            int[] descendants = MessageHierarchy.DescendantsByType[typeof(T)];
            for (int i = 0; i < descendants.Length; ++i)
            {
                var handlers = _handlers[descendants[i]];
                var messageHandler = handlers.FirstOrDefault(x => x.IsSame<T>(handler));
                if (messageHandler != null)
                    handlers.Remove(messageHandler);
            }
        }

        public void Handle(Message message)
        {
            Publish(message);
        }

        public void Publish(Message message)
        {
            if (message == null) throw new ArgumentNullException("message");

            var handlers = _handlers[message.MsgTypeId];
            for (int i = 0, n = handlers.Count; i < n; ++i)
            {
                var handler = handlers[i];
                if (_watchSlowMsg)
                {
                    var start = DateTime.UtcNow;

                    handler.TryHandle(message);

                    var elapsed = DateTime.UtcNow - start;
                    if (elapsed > _slowMsgThreshold)
                    {
                        Log.Trace("SLOW BUS MSG [{0}]: {1} - {2}ms. Handler: {3}.",
                                  Name, message.GetType().Name, (int)elapsed.TotalMilliseconds, handler.HandlerName);
                        if (elapsed > QueuedHandler.VerySlowMsgThreshold)
                            Log.Error("---!!! VERY SLOW BUS MSG [{0}]: {1} - {2}ms. Handler: {3}.",
                                      Name, message.GetType().Name, (int)elapsed.TotalMilliseconds, handler.HandlerName);
                    }
                }
                else
                {
                    handler.TryHandle(message);
                }
            }
        }
    }
}