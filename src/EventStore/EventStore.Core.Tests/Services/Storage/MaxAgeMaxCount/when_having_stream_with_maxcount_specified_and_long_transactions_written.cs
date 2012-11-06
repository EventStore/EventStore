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
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.MaxAgeMaxCount
{
    [TestFixture]
    public class when_having_stream_with_maxcount_specified_and_long_transactions_written : ReadIndexTestScenario
    {
        private EventRecord[] _records;

        protected override void WriteTestScenario()
        {
            const string metadata = @"{""$maxCount"":2}";

            _records = new EventRecord[10]; // 1 + 3 + 2 + 4
            _records[0] = WriteStreamCreated("ES", metadata);

            WriteTransaction(0, 3);
            WriteTransaction(3, 2);
            WriteTransaction(3 + 2, 4);
        }

        private void WriteTransaction(int expectedVersion, int transactionLength)
        {
            var begin = WriteTransactionBegin("ES", expectedVersion);
            for (int i = 0; i < transactionLength; ++i)
            {
                var eventNumber = expectedVersion + i + 1;
                _records[eventNumber] = WriteTransactionEvent(Guid.NewGuid(), begin.LogPosition, i, "ES", eventNumber, "data" + i, PrepareFlags.Data);
            }
            WriteTransactionEnd(Guid.NewGuid(), begin.LogPosition, "ES");
            WriteCommit(Guid.NewGuid(), begin.LogPosition, "ES", expectedVersion + 1);
        }

        [Test]
        public void forward_range_read_returns_last_transaction_events_and_doesnt_return_expired_ones()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsForward("ES", 0, 100, out records));
            Assert.AreEqual(2, records.Length);
            Assert.AreEqual(_records[8], records[0]);
            Assert.AreEqual(_records[9], records[1]);
        }
    }
}