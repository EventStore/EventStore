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
using System.Linq;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index
{
    [TestFixture]
    public class table_index_should : SpecificationWithDirectoryPerTestFixture
    {
        private TableIndex _tableIndex;

        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
            _tableIndex = new TableIndex(PathName,
                                         () => new HashListMemTable(maxSize: 20),
                                         () => { throw new InvalidOperationException(); },
                                         maxSizeForMemory: 10);
            _tableIndex.Initialize(long.MaxValue);
        }

        public override void TestFixtureTearDown()
        {
            _tableIndex.Close(); 
            base.TestFixtureTearDown();
        }

        [Test]
        public void throw_argumentoutofrangeexception_on_range_query_when_provided_with_negative_start_version()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _tableIndex.GetRange(0x0000, -1, int.MaxValue).ToArray());
        }

        [Test]
        public void throw_argumentoutofrangeexception_on_range_query_when_provided_with_negative_end_version()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _tableIndex.GetRange(0x0000, 0, -1).ToArray());
        }

        [Test]
        public void throw_argumentoutofrangeexception_on_get_one_entry_query_when_provided_with_negative_version()
        {
            long pos;
            Assert.Throws<ArgumentOutOfRangeException>(() => _tableIndex.TryGetOneValue(0x0000, -1, out pos));
        }

        [Test]
        public void throw_argumentoutofrangeexception_on_adding_entry_with_negative_commit_position()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _tableIndex.Add(-1, 0x0000, 0, 0));
        }

        [Test]
        public void throw_argumentoutofrangeexception_on_adding_entry_with_negative_version()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _tableIndex.Add(0, 0x0000, -1, 0));
        }

        [Test]
        public void throw_argumentoutofrangeexception_on_adding_entry_with_negative_position()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _tableIndex.Add(0, 0x0000, 0, -1));
        }
    }
}