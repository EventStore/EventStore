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
using EventStore.Core.Bus;

namespace EventStore.Core.Messaging
{
    public static class Forwarder
    {
        /// <summary>
        /// Creates a message handler publishing all incoming messages on destination.  
        /// </summary>
        public static IHandle<T> Create<T>(IPublisher to) where T : Message
        {
            return new F<T>(to);
        }

        /// <summary>
        /// Creates a message handler publishing all incoming messages onto one of the destinations.  
        /// </summary>
        public static IHandle<T> CreateBalancing<T>(params IPublisher[] to) where T : Message
        {
            return new Balancing<T>(to);
        }

        class F<T> : IHandle<T> where T : Message
        {
            private readonly IPublisher _to;

            public F(IPublisher to)
            {
                _to = to;
            }

            public void Handle(T message)
            {
                _to.Publish(message);
            }
        }

        class Balancing<T> : IHandle<T> where T : Message
        {
            private readonly IPublisher[] _to;
            private int _last;

            public Balancing(IPublisher[] to)
            {
                _to = to;
            }

            public void Handle(T message)
            {
                var last = _last;
                if (last == _to.Length - 1)
                    _last = 0;
                else
                    _last = last + 1;
                _to[_last].Publish(message);
            }
        }
    }
}
