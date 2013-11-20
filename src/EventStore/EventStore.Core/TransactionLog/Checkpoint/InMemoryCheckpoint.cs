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
using System.Threading;

namespace EventStore.Core.TransactionLog.Checkpoint
{
    public class InMemoryCheckpoint : ICheckpoint
    {
        public string Name { get { return _name; } }

        private long _last;
        private long _lastFlushed;
        private readonly string _name;

        private readonly object _flushLocker = new object();

        public InMemoryCheckpoint(long initialValue) : this(Guid.NewGuid().ToString(), initialValue) {}

        public InMemoryCheckpoint() : this(Guid.NewGuid().ToString(), 0) {}

        public InMemoryCheckpoint(string name, long initValue = 0)
        {
            _last = initValue;
            _lastFlushed = initValue;
            _name = name;
        }

        public void Write(long checkpoint)
        {
            Interlocked.Exchange(ref _last, checkpoint);
        }

        public long Read()
        {
            return Interlocked.Read(ref _lastFlushed);
        }

        public long ReadNonFlushed()
        {
            return Interlocked.Read(ref _last);
        }

        public void Flush()
        {
            var last = Interlocked.Read(ref _last);
            if (last == _lastFlushed)
                return;

            Interlocked.Exchange(ref _lastFlushed, last);

            lock (_flushLocker)
            {
                Monitor.PulseAll(_flushLocker);
            }
        }

        public bool WaitForFlush(TimeSpan timeout)
        {
            lock (_flushLocker)
            {
                return Monitor.Wait(_flushLocker, timeout);
            }
        }

        public void Close()
        {
            //NOOP
        }

        public void Dispose()
        {
            //NOOP
        }
    }
}