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
using EventStore.Core.Messages;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Projections.Core.Services.AwakeReaderService;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.awake_reader_service
{
    [TestFixture]
    public class when_handling_comitted_event
    {
        private AwakeReaderService _it;
        private EventRecord _eventRecord;
        private StorageMessage.EventCommited _eventCommited;
        private Exception _exception;

        [SetUp]
        public void SetUp()
        {
            _exception = null;
            Given();
            When();
        }

        private void Given()
        {
            _it = new AwakeReaderService();

            _eventRecord = new EventRecord(
                10,
                new PrepareLogRecord(
                    500, Guid.NewGuid(), Guid.NewGuid(), 500, 0, "Stream", 99, DateTime.UtcNow, PrepareFlags.Data,
                    "event", new byte[0], null));
            _eventCommited = new StorageMessage.EventCommited(1000, _eventRecord);
        }

        private void When()
        {
            try
            {
                _it.Handle(_eventCommited);
            }
            catch (Exception ex)
            {
                _exception = ex;
            }
        }

        [Test]
        public void it_is_handled()
        {
            Assert.IsNull(_exception, (_exception ?? (object)"").ToString());
        }

    }
}