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
using System.Text;
using System.Threading;
using EventStore.ClientAPI;
using EventStore.Core.Bus;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.ClientAPI
{
    [TestFixture, Category("LongRunning")]
    public class when_running_standard_projections : specification_with_standard_projections_runnning
    {
        [Test, Category("LongRunning"), Category("Network")]
        public void streams_stream_exists()
        {
            QueueStatsCollector.WaitIdle();
            Assert.AreEqual(
                SliceReadStatus.Success, _conn.ReadStreamEventsForward("$streams", 0, 10, false, _admin).Status);

        }

        [Test, Category("LongRunning"), Category("Network")]
        public void hard_deleted_stream_events_are_indexed()
        {
            var r1 = _conn.AppendToStream(
                "cat-1", ExpectedVersion.NoStream, _admin,
                new EventData(Guid.NewGuid(), "type1", true, Encoding.UTF8.GetBytes("{}"), null));

            var r2 = _conn.AppendToStream(
                "cat-1", r1.NextExpectedVersion, _admin,
                new EventData(Guid.NewGuid(), "type1", true, Encoding.UTF8.GetBytes("{}"), null));

            _conn.DeleteStream("cat-1", r2.NextExpectedVersion, true, _admin);
            QueueStatsCollector.WaitIdle();

            var slice = _conn.ReadStreamEventsForward("$ce-cat", 0, 10, true, _admin);
            Assert.AreEqual(SliceReadStatus.Success, slice.Status);

            Assert.AreEqual(3, slice.Events.Length);

        }

        [Test, Category("LongRunning"), Category("Network")]
        public void soft_deleted_stream_events_are_indexed()
        {
            var r1 = _conn.AppendToStream(
                "cat2-1", ExpectedVersion.NoStream, _admin,
                new EventData(Guid.NewGuid(), "type1", true, Encoding.UTF8.GetBytes("{}"), null));

            var r2 = _conn.AppendToStream(
                "cat2-1", r1.NextExpectedVersion, _admin,
                new EventData(Guid.NewGuid(), "type1", true, Encoding.UTF8.GetBytes("{}"), null));

            _conn.DeleteStream("cat2-1", r2.NextExpectedVersion, false, _admin);
            QueueStatsCollector.WaitIdle();

            var slice = _conn.ReadStreamEventsForward("$ce-cat2", 0, 10, true, _admin);
            Assert.AreEqual(SliceReadStatus.Success, slice.Status);

            Assert.AreEqual(3, slice.Events.Length);

        }
    }
}
