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
using System.IO;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index
{
    [TestFixture]
    public class destroying_ptable: SpecificationWithFile
    {
        private PTable _table;

        [SetUp]
        public void Setup()
        {
            var mtable = new HashListMemTable(maxSize: 10);
            mtable.Add(0x0101, 0x0001, 0x0001);
            mtable.Add(0x0105, 0x0001, 0x0002);
            _table = PTable.FromMemtable(mtable, Filename);
            _table.MarkForDestruction();
        }

        [Test]
        public void the_file_is_deleted()
        {
            _table.WaitForDisposal(1000);
            Assert.IsFalse(File.Exists(Filename));
        }

        [Test]
        public void wait_for_destruction_returns()
        {
            Assert.DoesNotThrow(() => _table.WaitForDisposal(1000));
        }

        [TearDown]
        public void Teardown()
        {
            _table.WaitForDisposal(1000);
            File.Delete(Filename);
        }
    }
}