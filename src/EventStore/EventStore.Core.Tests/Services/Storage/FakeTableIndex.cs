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
using EventStore.Core.Index;

namespace EventStore.Core.Tests.Services.Storage
{
    public class FakeTableIndex: ITableIndex
    {
        public long PrepareCheckpoint { get { throw new NotImplementedException(); } }
        public long CommitCheckpoint { get { throw new NotImplementedException(); } }

        public void Initialize(long writerCheckpoint)
        {
        }

        public void Close(bool removeFiles = true)
        {
        }

        public void Add(long commitPos, uint stream, int version, long position)
        {
            throw new NotImplementedException();
        }

        public void AddEntries(long commitPos, IList<IndexEntry> entries)
        {
            throw new NotImplementedException();
        }

        public bool TryGetOneValue(uint stream, int version, out long position)
        {
            throw new NotImplementedException();
        }

        public bool TryGetLatestEntry(uint stream, out IndexEntry entry)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IndexEntry> GetRange(uint stream, int startVersion, int endVersion)
        {
            throw new NotImplementedException();
        }
    }
}