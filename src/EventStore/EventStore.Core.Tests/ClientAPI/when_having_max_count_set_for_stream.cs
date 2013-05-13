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
using EventStore.ClientAPI;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helper;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("LongRunning")]
    public class when_having_max_count_set_for_stream : SpecificationWithDirectory
    {
        private const string Stream = "max-count-test-stream";

        private MiniNode _node;
        private IEventStoreConnection _connection;
        private EventData[] _testEvents;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            _node = new MiniNode(PathName);
            _node.Start();

            _connection = TestConnection.Create(_node.TcpEndPoint);
            _connection.Connect();

            _connection.SetStreamMetadata(Stream,
                                          ExpectedVersion.EmptyStream,
                                          Guid.NewGuid(),
                                          StreamMetadata.Build().SetMaxCount(3));

            _testEvents = Enumerable.Range(0, 5).Select(x => TestEvent.NewTestEvent(data: x.ToString())).ToArray();
            _connection.AppendToStream(Stream, ExpectedVersion.EmptyStream, _testEvents);
        }

        [TearDown]
        public override void TearDown()
        {
            _connection.Close();
            _node.Shutdown();
            base.TearDown();
        }

        [Test]
        public void read_stream_forward_respects_max_count()
        {
            var res = _connection.ReadStreamEventsForward(Stream, 0, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(3, res.Events.Length);
            Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(), 
                            res.Events.Select(x => x.Event.EventId).ToArray());
        }

        [Test]
        public void read_stream_backward_respects_max_count()
        {
            var res = _connection.ReadStreamEventsBackward(Stream, -1, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(3, res.Events.Length);
            Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
                            res.Events.Reverse().Select(x => x.Event.EventId).ToArray());
        }

        [Test]
        public void read_all_forward_does_not_care()
        {
            var res = _connection.ReadAllEventsForward(Position.Start, 100, false);
            Assert.AreEqual(5 + 1, res.Events.Length); // metaevent as well
            Assert.AreEqual(_testEvents.Select(x => x.EventId).ToArray(),
                            res.Events.Skip(1).Select(x => x.Event.EventId).ToArray()); // skip metaevent
        }

        [Test]
        public void read_all_backward_does_not_care()
        {
            var res = _connection.ReadAllEventsBackward(Position.End, 100, false);
            Assert.AreEqual(5 + 1, res.Events.Length); // metaevent as well
            Assert.AreEqual(_testEvents.Select(x => x.EventId).ToArray(),
                            res.Events.Reverse().Skip(1).Select(x => x.Event.EventId).ToArray()); // skip metaevent
        }

        [Test]
        public void after_setting_less_strict_max_count_read_stream_forward_reads_more_events()
        {
            var res = _connection.ReadStreamEventsForward(Stream, 0, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(3, res.Events.Length);
            Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
                            res.Events.Select(x => x.Event.EventId).ToArray());

            _connection.SetStreamMetadata(Stream, 0, Guid.NewGuid(), StreamMetadata.Build().SetMaxCount(4));

            res = _connection.ReadStreamEventsForward(Stream, 0, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(4, res.Events.Length);
            Assert.AreEqual(_testEvents.Skip(1).Select(x => x.EventId).ToArray(),
                            res.Events.Select(x => x.Event.EventId).ToArray());
        }

        [Test]
        public void after_setting_more_strict_max_count_read_stream_forward_reads_less_events()
        {
            var res = _connection.ReadStreamEventsForward(Stream, 0, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(3, res.Events.Length);
            Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
                            res.Events.Select(x => x.Event.EventId).ToArray());

            _connection.SetStreamMetadata(Stream, 0, Guid.NewGuid(), StreamMetadata.Build().SetMaxCount(2));

            res = _connection.ReadStreamEventsForward(Stream, 0, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(2, res.Events.Length);
            Assert.AreEqual(_testEvents.Skip(3).Select(x => x.EventId).ToArray(),
                            res.Events.Select(x => x.Event.EventId).ToArray());
        }

        [Test]
        public void after_setting_less_strict_max_count_read_stream_backward_reads_more_events()
        {
            var res = _connection.ReadStreamEventsBackward(Stream, -1, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(3, res.Events.Length);
            Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
                            res.Events.Reverse().Select(x => x.Event.EventId).ToArray());

            _connection.SetStreamMetadata(Stream, 0, Guid.NewGuid(), StreamMetadata.Build().SetMaxCount(4));

            res = _connection.ReadStreamEventsBackward(Stream, -1, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(4, res.Events.Length);
            Assert.AreEqual(_testEvents.Skip(1).Select(x => x.EventId).ToArray(),
                            res.Events.Reverse().Select(x => x.Event.EventId).ToArray());
        }

        [Test]
        public void after_setting_more_strict_max_count_read_stream_backward_reads_less_events()
        {
            var res = _connection.ReadStreamEventsBackward(Stream, -1, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(3, res.Events.Length);
            Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
                            res.Events.Reverse().Select(x => x.Event.EventId).ToArray());

            _connection.SetStreamMetadata(Stream, 0, Guid.NewGuid(), StreamMetadata.Build().SetMaxCount(2));

            res = _connection.ReadStreamEventsBackward(Stream, -1, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(2, res.Events.Length);
            Assert.AreEqual(_testEvents.Skip(3).Select(x => x.EventId).ToArray(),
                            res.Events.Reverse().Select(x => x.Event.EventId).ToArray());
        }
    }
}
