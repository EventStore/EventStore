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

using EventStore.ClientAPI;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Helpers
{
    internal class StreamWriter
    {
        private readonly IEventStoreConnection _store;
        private readonly string _stream;
        private readonly int _version;

        public StreamWriter(IEventStoreConnection store, string stream, int version)
        {
            _store = store;
            _stream = stream;
            _version = version;
        }

        public TailWriter Append(params EventData[] events)
        {
            for (var i = 0; i < events.Length; i++)
            {
                var expVer = _version == ExpectedVersion.Any ? ExpectedVersion.Any : _version + i;
                var nextExpVer = _store.AppendToStream(_stream, expVer, new[] {events[i]});
                if (_version != ExpectedVersion.Any)
                    Assert.AreEqual(expVer + 1, nextExpVer);
            }
            return new TailWriter(_store, _stream);
        }
    }

    internal class TailWriter
    {
        private readonly IEventStoreConnection _store;
        private readonly string _stream;

        public TailWriter(IEventStoreConnection store, string stream)
        {
            _store = store;
            _stream = stream;
        }

        public TailWriter Then(EventData @event, int expectedVersion)
        {
            _store.AppendToStream(_stream, expectedVersion, new[] {@event});
            return this;
        }
    }

    internal class TransactionalWriter
    {
        private readonly IEventStoreConnection _store;
        private readonly string _stream;

        public TransactionalWriter(IEventStoreConnection store, string stream)
        {
            _store = store;
            _stream = stream;
        }

        public OngoingTransaction StartTransaction(int expectedVersion)
        {
            return new OngoingTransaction(_store.StartTransaction(_stream, expectedVersion));
        }
    }

    //TODO GFY this should be removed and merged with the public idea of a transaction.
    internal class OngoingTransaction
    {
        private readonly EventStoreTransaction _transaction;

        public OngoingTransaction(EventStoreTransaction transaction)
        {
            _transaction = transaction;
        }

        public OngoingTransaction Write(params EventData[] events)
        {
            _transaction.Write(events);
            return this;
        }

        public int Commit()
        {
            return _transaction.Commit();
        }
    }
}
