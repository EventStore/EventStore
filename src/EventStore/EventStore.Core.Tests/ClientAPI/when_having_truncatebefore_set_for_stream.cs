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

using System.Linq;
using EventStore.ClientAPI;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("LongRunning")]
    public class when_having_truncatebefore_set_for_stream : SpecificationWithDirectoryPerTestFixture
    {
        private MiniNode _node;
        private IEventStoreConnection _connection;
        private EventData[] _testEvents;

        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
            _node = new MiniNode(PathName);
            _node.Start();

            _connection = TestConnection.Create(_node.TcpEndPoint);
            _connection.Connect();

            _testEvents = Enumerable.Range(0, 5).Select(x => TestEvent.NewTestEvent(data: x.ToString())).ToArray();
        }

        [TestFixtureTearDown]
        public override void TestFixtureTearDown()
        {
            _connection.Close();
            _node.Shutdown();
            base.TestFixtureTearDown();
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void read_event_respects_truncatebefore()
        {
            const string stream = "read_event_respects_truncatebefore";
            _connection.AppendToStream(stream, ExpectedVersion.EmptyStream, _testEvents);

            _connection.SetStreamMetadata(stream, ExpectedVersion.EmptyStream, StreamMetadata.Build().SetTruncateBefore(2));

            var res = _connection.ReadEvent(stream, 1, false);
            Assert.AreEqual(EventReadStatus.NotFound, res.Status);

            res = _connection.ReadEvent(stream, 2, false);
            Assert.AreEqual(EventReadStatus.Success, res.Status);
            Assert.AreEqual(_testEvents[2].EventId, res.Event.Value.OriginalEvent.EventId);
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void read_stream_forward_respects_truncatebefore()
        {
            const string stream = "read_stream_forward_respects_truncatebefore";
            _connection.AppendToStream(stream, ExpectedVersion.EmptyStream, _testEvents);

            _connection.SetStreamMetadata(stream, ExpectedVersion.EmptyStream, StreamMetadata.Build().SetTruncateBefore(2));

            var res = _connection.ReadStreamEventsForward(stream, 0, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(3, res.Events.Length);
            Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(), 
                            res.Events.Select(x => x.Event.EventId).ToArray());
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void read_stream_backward_respects_truncatebefore()
        {
            const string stream = "read_stream_backward_respects_truncatebefore";
            _connection.AppendToStream(stream, ExpectedVersion.EmptyStream, _testEvents);

            _connection.SetStreamMetadata(stream, ExpectedVersion.EmptyStream, StreamMetadata.Build().SetTruncateBefore(2));

            var res = _connection.ReadStreamEventsBackward(stream, -1, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(3, res.Events.Length);
            Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
                            res.Events.Reverse().Select(x => x.Event.EventId).ToArray());
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void after_setting_less_strict_truncatebefore_read_event_reads_more_events()
        {
            const string stream = "after_setting_less_strict_truncatebefore_read_event_reads_more_events";

            _connection.AppendToStream(stream, ExpectedVersion.EmptyStream, _testEvents);

            _connection.SetStreamMetadata(stream, ExpectedVersion.EmptyStream, StreamMetadata.Build().SetTruncateBefore(2));

            var res = _connection.ReadEvent(stream, 1, false);
            Assert.AreEqual(EventReadStatus.NotFound, res.Status);

            res = _connection.ReadEvent(stream, 2, false);
            Assert.AreEqual(EventReadStatus.Success, res.Status);
            Assert.AreEqual(_testEvents[2].EventId, res.Event.Value.OriginalEvent.EventId);

            _connection.SetStreamMetadata(stream, 0, StreamMetadata.Build().SetTruncateBefore(1));

            res = _connection.ReadEvent(stream, 0, false);
            Assert.AreEqual(EventReadStatus.NotFound, res.Status);

            res = _connection.ReadEvent(stream, 1, false);
            Assert.AreEqual(EventReadStatus.Success, res.Status);
            Assert.AreEqual(_testEvents[1].EventId, res.Event.Value.OriginalEvent.EventId);
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void after_setting_more_strict_truncatebefore_read_event_reads_less_events()
        {
            const string stream = "after_setting_more_strict_truncatebefore_read_event_reads_less_events";

            _connection.AppendToStream(stream, ExpectedVersion.EmptyStream, _testEvents);

            _connection.SetStreamMetadata(stream, ExpectedVersion.EmptyStream, StreamMetadata.Build().SetTruncateBefore(2));

            var res = _connection.ReadEvent(stream, 1, false);
            Assert.AreEqual(EventReadStatus.NotFound, res.Status);

            res = _connection.ReadEvent(stream, 2, false);
            Assert.AreEqual(EventReadStatus.Success, res.Status);
            Assert.AreEqual(_testEvents[2].EventId, res.Event.Value.OriginalEvent.EventId);

            _connection.SetStreamMetadata(stream, 0, StreamMetadata.Build().SetTruncateBefore(3));

            res = _connection.ReadEvent(stream, 2, false);
            Assert.AreEqual(EventReadStatus.NotFound, res.Status);

            res = _connection.ReadEvent(stream, 3, false);
            Assert.AreEqual(EventReadStatus.Success, res.Status);
            Assert.AreEqual(_testEvents[3].EventId, res.Event.Value.OriginalEvent.EventId);
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void less_strict_max_count_doesnt_change_anything_for_event_read()
        {
            const string stream = "less_strict_max_count_doesnt_change_anything_for_event_read";

            _connection.AppendToStream(stream, ExpectedVersion.EmptyStream, _testEvents);

            _connection.SetStreamMetadata(stream, ExpectedVersion.EmptyStream, StreamMetadata.Build().SetTruncateBefore(2));

            var res = _connection.ReadEvent(stream, 1, false);
            Assert.AreEqual(EventReadStatus.NotFound, res.Status);

            res = _connection.ReadEvent(stream, 2, false);
            Assert.AreEqual(EventReadStatus.Success, res.Status);
            Assert.AreEqual(_testEvents[2].EventId, res.Event.Value.OriginalEvent.EventId);

            _connection.SetStreamMetadata(stream, 0, StreamMetadata.Build().SetTruncateBefore(2).SetMaxCount(4));

            res = _connection.ReadEvent(stream, 1, false);
            Assert.AreEqual(EventReadStatus.NotFound, res.Status);

            res = _connection.ReadEvent(stream, 2, false);
            Assert.AreEqual(EventReadStatus.Success, res.Status);
            Assert.AreEqual(_testEvents[2].EventId, res.Event.Value.OriginalEvent.EventId);
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void more_strict_max_count_gives_less_events_for_event_read()
        {
            const string stream = "more_strict_max_count_gives_less_events_for_event_read";

            _connection.AppendToStream(stream, ExpectedVersion.EmptyStream, _testEvents);

            _connection.SetStreamMetadata(stream, ExpectedVersion.EmptyStream, StreamMetadata.Build().SetTruncateBefore(2));

            var res = _connection.ReadEvent(stream, 1, false);
            Assert.AreEqual(EventReadStatus.NotFound, res.Status);

            res = _connection.ReadEvent(stream, 2, false);
            Assert.AreEqual(EventReadStatus.Success, res.Status);
            Assert.AreEqual(_testEvents[2].EventId, res.Event.Value.OriginalEvent.EventId);

            _connection.SetStreamMetadata(stream, 0, StreamMetadata.Build().SetTruncateBefore(2).SetMaxCount(2));

            res = _connection.ReadEvent(stream, 2, false);
            Assert.AreEqual(EventReadStatus.NotFound, res.Status);

            res = _connection.ReadEvent(stream, 3, false);
            Assert.AreEqual(EventReadStatus.Success, res.Status);
            Assert.AreEqual(_testEvents[3].EventId, res.Event.Value.OriginalEvent.EventId);
        }


        [Test, Category("LongRunning"), Category("Network")]
        public void after_setting_less_strict_truncatebefore_read_stream_forward_reads_more_events()
        {
            const string stream = "after_setting_less_strict_truncatebefore_read_stream_forward_reads_more_events";

            _connection.AppendToStream(stream, ExpectedVersion.EmptyStream, _testEvents);

            _connection.SetStreamMetadata(stream, ExpectedVersion.EmptyStream, StreamMetadata.Build().SetTruncateBefore(2));

            var res = _connection.ReadStreamEventsForward(stream, 0, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(3, res.Events.Length);
            Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
                            res.Events.Select(x => x.Event.EventId).ToArray());

            _connection.SetStreamMetadata(stream, 0, StreamMetadata.Build().SetTruncateBefore(1));

            res = _connection.ReadStreamEventsForward(stream, 0, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(4, res.Events.Length);
            Assert.AreEqual(_testEvents.Skip(1).Select(x => x.EventId).ToArray(),
                            res.Events.Select(x => x.Event.EventId).ToArray());
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void after_setting_more_strict_truncatebefore_read_stream_forward_reads_less_events()
        {
            const string stream = "after_setting_more_strict_truncatebefore_read_stream_forward_reads_less_events";

            _connection.AppendToStream(stream, ExpectedVersion.EmptyStream, _testEvents);

            _connection.SetStreamMetadata(stream, ExpectedVersion.EmptyStream, StreamMetadata.Build().SetTruncateBefore(2));

            var res = _connection.ReadStreamEventsForward(stream, 0, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(3, res.Events.Length);
            Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
                            res.Events.Select(x => x.Event.EventId).ToArray());

            _connection.SetStreamMetadata(stream, 0, StreamMetadata.Build().SetTruncateBefore(3));

            res = _connection.ReadStreamEventsForward(stream, 0, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(2, res.Events.Length);
            Assert.AreEqual(_testEvents.Skip(3).Select(x => x.EventId).ToArray(),
                            res.Events.Select(x => x.Event.EventId).ToArray());
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void less_strict_max_count_doesnt_change_anything_for_stream_forward_read()
        {
            const string stream = "less_strict_max_count_doesnt_change_anything_for_stream_forward_read";

            _connection.AppendToStream(stream, ExpectedVersion.EmptyStream, _testEvents);

            _connection.SetStreamMetadata(stream, ExpectedVersion.EmptyStream, StreamMetadata.Build().SetTruncateBefore(2));

            var res = _connection.ReadStreamEventsForward(stream, 0, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(3, res.Events.Length);
            Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
                            res.Events.Select(x => x.Event.EventId).ToArray());

            _connection.SetStreamMetadata(stream, 0, StreamMetadata.Build().SetTruncateBefore(2).SetMaxCount(4));

            res = _connection.ReadStreamEventsForward(stream, 0, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(3, res.Events.Length);
            Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
                            res.Events.Select(x => x.Event.EventId).ToArray());
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void more_strict_max_count_gives_less_events_for_stream_forward_read()
        {
            const string stream = "more_strict_max_count_gives_less_events_for_stream_forward_read";

            _connection.AppendToStream(stream, ExpectedVersion.EmptyStream, _testEvents);

            _connection.SetStreamMetadata(stream, ExpectedVersion.EmptyStream, StreamMetadata.Build().SetTruncateBefore(2));

            var res = _connection.ReadStreamEventsForward(stream, 0, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(3, res.Events.Length);
            Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
                            res.Events.Select(x => x.Event.EventId).ToArray());

            _connection.SetStreamMetadata(stream, 0, StreamMetadata.Build().SetTruncateBefore(2).SetMaxCount(2));

            res = _connection.ReadStreamEventsForward(stream, 0, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(2, res.Events.Length);
            Assert.AreEqual(_testEvents.Skip(3).Select(x => x.EventId).ToArray(),
                            res.Events.Select(x => x.Event.EventId).ToArray());
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void after_setting_less_strict_truncatebefore_read_stream_backward_reads_more_events()
        {
            const string stream = "after_setting_less_strict_truncatebefore_read_stream_backward_reads_more_events";

            _connection.AppendToStream(stream, ExpectedVersion.EmptyStream, _testEvents);

            _connection.SetStreamMetadata(stream, ExpectedVersion.EmptyStream, StreamMetadata.Build().SetTruncateBefore(2));

            var res = _connection.ReadStreamEventsBackward(stream, -1, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(3, res.Events.Length);
            Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
                            res.Events.Reverse().Select(x => x.Event.EventId).ToArray());

            _connection.SetStreamMetadata(stream, 0, StreamMetadata.Build().SetTruncateBefore(1));

            res = _connection.ReadStreamEventsBackward(stream, -1, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(4, res.Events.Length);
            Assert.AreEqual(_testEvents.Skip(1).Select(x => x.EventId).ToArray(),
                            res.Events.Reverse().Select(x => x.Event.EventId).ToArray());
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void after_setting_more_strict_truncatebefore_read_stream_backward_reads_less_events()
        {
            const string stream = "after_setting_more_strict_truncatebefore_read_stream_backward_reads_less_events";

            _connection.AppendToStream(stream, ExpectedVersion.EmptyStream, _testEvents);

            _connection.SetStreamMetadata(stream, ExpectedVersion.EmptyStream, StreamMetadata.Build().SetTruncateBefore(2));

            var res = _connection.ReadStreamEventsBackward(stream, -1, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(3, res.Events.Length);
            Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
                            res.Events.Reverse().Select(x => x.Event.EventId).ToArray());

            _connection.SetStreamMetadata(stream, 0, StreamMetadata.Build().SetTruncateBefore(3));

            res = _connection.ReadStreamEventsBackward(stream, -1, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(2, res.Events.Length);
            Assert.AreEqual(_testEvents.Skip(3).Select(x => x.EventId).ToArray(),
                            res.Events.Reverse().Select(x => x.Event.EventId).ToArray());
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void less_strict_max_count_doesnt_change_anything_for_stream_backward_read()
        {
            const string stream = "less_strict_max_count_doesnt_change_anything_for_stream_backward_read";

            _connection.AppendToStream(stream, ExpectedVersion.EmptyStream, _testEvents);

            _connection.SetStreamMetadata(stream, ExpectedVersion.EmptyStream, StreamMetadata.Build().SetTruncateBefore(2));

            var res = _connection.ReadStreamEventsBackward(stream, -1, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(3, res.Events.Length);
            Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
                            res.Events.Reverse().Select(x => x.Event.EventId).ToArray());

            _connection.SetStreamMetadata(stream, 0, StreamMetadata.Build().SetTruncateBefore(2).SetMaxCount(4));

            res = _connection.ReadStreamEventsBackward(stream, -1, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(3, res.Events.Length);
            Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
                            res.Events.Reverse().Select(x => x.Event.EventId).ToArray());
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void more_strict_max_count_gives_less_events_for_stream_backward_read()
        {
            const string stream = "more_strict_max_count_gives_less_events_for_stream_backward_read";

            _connection.AppendToStream(stream, ExpectedVersion.EmptyStream, _testEvents);

            _connection.SetStreamMetadata(stream, ExpectedVersion.EmptyStream, StreamMetadata.Build().SetTruncateBefore(2));

            var res = _connection.ReadStreamEventsBackward(stream, -1, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(3, res.Events.Length);
            Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
                            res.Events.Reverse().Select(x => x.Event.EventId).ToArray());

            _connection.SetStreamMetadata(stream, 0, StreamMetadata.Build().SetTruncateBefore(2).SetMaxCount(2));

            res = _connection.ReadStreamEventsBackward(stream, -1, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(2, res.Events.Length);
            Assert.AreEqual(_testEvents.Skip(3).Select(x => x.EventId).ToArray(),
                            res.Events.Reverse().Select(x => x.Event.EventId).ToArray());
        }
    }
}
