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

using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging
{
    [TestFixture]
    public class when_metastream_is_scavenged_and_read_index_is_set_to_keep_just_last_metaevent : ScavengeTestScenario
    {
        protected override DbResult CreateDb(TFChunkDbCreationHelper dbCreator)
        {
            return dbCreator
                .Chunk(Rec.Prepare(0, "$$bla", metadata: new StreamMetadata(10, null, null, null, null)),
                       Rec.Prepare(0, "$$bla", metadata: new StreamMetadata(5, null, null, null, null)),
                       Rec.Prepare(0, "$$bla", metadata: new StreamMetadata(3, null, null, null, null)),
                       Rec.Prepare(0, "$$bla", metadata: new StreamMetadata(2, null, null, null, null)),
                       Rec.Commit(0, "$$bla"))
                .CompleteLastChunk()
                .CreateDb();
        }

        protected override LogRecord[][] KeptRecords(DbResult dbResult)
        {
            return new[]
            {
                dbResult.Recs[0].Where((x, i) => i >= 3).ToArray()
            };
        }

        [Test]
        public void only_last_metaevent_is_left()
        {
            CheckRecords();
        }
    }
}