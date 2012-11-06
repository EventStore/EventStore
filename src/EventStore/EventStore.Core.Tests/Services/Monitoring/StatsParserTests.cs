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
using EventStore.Core.Services.Monitoring.Stats;
using EventStore.Core.Tests.Fakes;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Monitoring
{
    [TestFixture]
    public class IoParserTests
    {
        private readonly string ioStr = "rchar: 23550615" + Environment.NewLine +
                                        "wchar: 290654" + Environment.NewLine +
                                        "syscr: 184391" + Environment.NewLine +
                                        "syscw: 3273" + Environment.NewLine +
                                        "read_bytes: 13824000" + Environment.NewLine +
                                        "write_bytes: 188416" + Environment.NewLine +
                                        "cancelled_write_bytes: 0" + Environment.NewLine;

        [Test]
        public void sample_io_doesnt_crash()
        {
            var io = DiskIo.ParseOnLinux(ioStr, new FakeLogger());
            var success = io != null;

            Assert.That(success, Is.True);
        }

        [Test]
        public void bad_io_crashes()
        {
            var badIoStr = ioStr.Remove(5, 20);

            DiskIo io = DiskIo.ParseOnLinux(badIoStr, new FakeLogger());
            var success = io != null;

            Assert.That(success, Is.False);
        }

        [Test]
        public void read_bytes_parses_ok()
        {
            var io = DiskIo.ParseOnLinux(ioStr, new FakeLogger());

            Assert.That(io.ReadBytes, Is.EqualTo(13824000));
        }

        [Test]
        public void write_bytes_parses_ok()
        {
            var io = DiskIo.ParseOnLinux(ioStr, new FakeLogger());

            Assert.That(io.WrittenBytes, Is.EqualTo(188416));
        }

    }
}
