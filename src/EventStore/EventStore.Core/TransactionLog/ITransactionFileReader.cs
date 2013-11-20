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
using EventStore.Core.DataStructures;

namespace EventStore.Core.TransactionLog
{
    public interface ITransactionFileReader
    {
        void Reposition(long position);

        SeqReadResult TryReadNext();
        SeqReadResult TryReadPrev();

        RecordReadResult TryReadAt(long position);
        bool ExistsAt(long position);
    }

    public struct TFReaderLease : IDisposable
    {
        public readonly ITransactionFileReader Reader;
        private readonly ObjectPool<ITransactionFileReader> _pool;

        public TFReaderLease(ObjectPool<ITransactionFileReader> pool)
        {
            _pool = pool;
            Reader = pool.Get();
        }

        public TFReaderLease(ITransactionFileReader reader)
        {
            _pool = null;
            Reader = reader;
        }

        void IDisposable.Dispose()
        {
            if (_pool != null)
                _pool.Return(Reader);
        }

        public void Reposition(long position)
        {
            Reader.Reposition(position);
        }

        public SeqReadResult TryReadNext()
        {
            return Reader.TryReadNext();
        }

        public SeqReadResult TryReadPrev()
        {
            return Reader.TryReadPrev();
        }

        public bool ExistsAt(long position)
        {
            return Reader.ExistsAt(position);
        }

        public RecordReadResult TryReadAt(long position)
        {
            return Reader.TryReadAt(position);
        }
    }
}