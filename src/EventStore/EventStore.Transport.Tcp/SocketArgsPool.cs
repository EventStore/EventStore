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
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace EventStore.Transport.Tcp
{
    internal class SocketArgsPool
    {
        public readonly string Name;

        private readonly Func<SocketAsyncEventArgs> _socketArgsCreator;
        private readonly ConcurrentStack<SocketAsyncEventArgs> _socketArgsPool = new ConcurrentStack<SocketAsyncEventArgs>();

        public SocketArgsPool(string name, int initialCount, Func<SocketAsyncEventArgs> socketArgsCreator)
        {
            if (socketArgsCreator == null)
                throw new ArgumentNullException("socketArgsCreator");
            if (initialCount < 0)
                throw new ArgumentOutOfRangeException("initialCount");

            Name = name;
            _socketArgsCreator = socketArgsCreator;

            for (int i = 0; i < initialCount; ++i)
            {
                _socketArgsPool.Push(socketArgsCreator());
            }
        }

        public SocketAsyncEventArgs Get()
        {
            SocketAsyncEventArgs result;
            if (_socketArgsPool.TryPop(out result))
                return result;
            return _socketArgsCreator();
        }

        public void Return(SocketAsyncEventArgs socketArgs)
        {
            _socketArgsPool.Push(socketArgs);
        }
    }
}