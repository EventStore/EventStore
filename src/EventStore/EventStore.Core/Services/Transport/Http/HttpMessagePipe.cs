﻿// Copyright (c) 2012, Event Store LLP
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
using System.Collections.Concurrent;
using System.Net;
using EventStore.Common.Utils;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.Transport.Http
{
    public class HttpMessagePipe
    {
        private readonly ConcurrentDictionary<Type, IMessageSender> _senders = new ConcurrentDictionary<Type, IMessageSender>();

        public void RegisterSender<T>(ISender<T> sender) where T : Message
        {
            Ensure.NotNull(sender, "sender");
            _senders.TryAdd(typeof (T), new MessageSender<T>(sender));
        }

        public void Push(Message message, IPEndPoint endPoint)
        {
            Ensure.NotNull(message, "message");
            Ensure.NotNull(endPoint, "endPoint");

            var type = message.GetType();
            IMessageSender sender;

            if (_senders.TryGetValue(type, out sender))
                sender.Send(message, endPoint);
        }
    }

    public interface ISender<in T> where T : Message
    {
        void Send(T message, IPEndPoint endPoint);
    }

    public interface IMessageSender
    {
        void Send(Message message, IPEndPoint endPoint);
    }

    public class MessageSender<T> : IMessageSender 
        where T : Message
    {
        private readonly ISender<T> _sender;

        public MessageSender(ISender<T> sender)
        {
            Ensure.NotNull(sender, "sender");
            _sender = sender;
        }

        public void Send(Message message, IPEndPoint endPoint)
        {
            Ensure.NotNull(message, "message");
            Ensure.NotNull(endPoint, "endPoint");

            _sender.Send((T) message, endPoint);
        }
    }
}