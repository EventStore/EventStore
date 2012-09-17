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
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog
{
    [TestFixture]
    public class prepare_log_record_should
    {
        [Test, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void throw_argumentoutofrangeexception_when_given_negative_logposition()
        {
            new PrepareLogRecord(-1, Guid.Empty, Guid.NewGuid(), -1, "test", 0, DateTime.UtcNow,
                                 PrepareFlags.None, "type", new byte[0], null);
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void throw_argumentexception_when_given_empty_correlationid()
        {
            new PrepareLogRecord(0, Guid.Empty, Guid.NewGuid(), 0, "test", 0, DateTime.UtcNow, 
                                 PrepareFlags.None, "type", new byte[0], null);
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void throw_argumentexception_when_given_empty_eventid()
        {
            new PrepareLogRecord(0, Guid.NewGuid(), Guid.Empty, 0, "test", 0, DateTime.UtcNow, 
                                 PrepareFlags.None, "type", new byte[0], null);
        }

        [Test, ExpectedException(typeof(ArgumentNullException))]
        public void throw_argumentnullexception_when_given_null_eventstreamid()
        {
            new PrepareLogRecord(0, Guid.NewGuid(), Guid.NewGuid(), 0, null, 0, DateTime.UtcNow, 
                                 PrepareFlags.None, "type", new byte[0], null);
        }

        [Test, ExpectedException(typeof(ArgumentNullException))]
        public void throw_argumentnullexception_when_given_empty_eventstreamid()
        {
            new PrepareLogRecord(0, Guid.NewGuid(), Guid.NewGuid(), 0, string.Empty, 0, DateTime.UtcNow, 
                                 PrepareFlags.None, "type", new byte[0], null);
        }

        [Test, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void throw_argumentoutofrangeexception_when_given_incorrect_expectedversion()
        {
            new PrepareLogRecord(0, Guid.NewGuid(), Guid.NewGuid(), 0, "test", -3, DateTime.UtcNow, 
                                 PrepareFlags.None, "type", new byte[0], null);
        }

        [Test, ExpectedException(typeof(ArgumentNullException))]
        public void throw_argumentnullexception_when_given_null_data()
        {
            new PrepareLogRecord(0, Guid.NewGuid(), Guid.NewGuid(), 0, "test", 0, DateTime.UtcNow, 
                                 PrepareFlags.None, "type", null, null);
        }

        [Test]
        public void throw_argumentnullexception_when_given_null_eventtype()
        {
            Assert.DoesNotThrow(() => new PrepareLogRecord(0, Guid.NewGuid(), Guid.NewGuid(), 0, "test", 0, DateTime.UtcNow, 
                                                           PrepareFlags.None, null, new byte[0], null));
        }

        [Test]
        public void throw_argumentexception_when_given_empty_eventtype()
        {
            Assert.DoesNotThrow(() => new PrepareLogRecord(0, Guid.NewGuid(), Guid.NewGuid(), 0, "test", 0, DateTime.UtcNow,
                                                           PrepareFlags.None, string.Empty, new byte[0], null));
        }
    }
}