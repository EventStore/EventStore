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
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using EventStore.Core.Tests.ClientAPI.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("LongRunning")]
    public class when_committing_empty_transaction : SpecificationWithDirectory
    {
        private MiniNode _node;
        private EventStoreConnection _connection;
        private EventData _firstEvent;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            _node = new MiniNode(PathName);
            _node.Start();

            _firstEvent = TestEvent.NewTestEvent();

            _connection = EventStoreConnection.Create();
            _connection.Connect(_node.TcpEndPoint);

            _connection.AppendToStream("test-stream",
                                       ExpectedVersion.NoStream,
                                       _firstEvent,
                                       TestEvent.NewTestEvent(),
                                       TestEvent.NewTestEvent());

            using (var transaction = _connection.StartTransaction("test-stream", 3))
            {
                transaction.Commit();
            }
        }

        [TearDown]
        public override void TearDown()
        {
            _connection.Close();
            _node.Shutdown();
            base.TearDown();
        }

        [Test]
        public void following_append_with_correct_expected_version_are_commited_correctly()
        {
            _connection.AppendToStream("test-stream", 3, TestEvent.NewTestEvent(), TestEvent.NewTestEvent());

            var res = _connection.ReadStreamEventsForward("test-stream", 0, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(5+1, res.Events.Length);
            for (int i=0; i<6; ++i)
            {
                Assert.AreEqual(i, res.Events[i].OriginalEventNumber);
            }
        }

        [Test]
        public void following_append_with_expected_version_any_are_commited_correctly()
        {
            _connection.AppendToStream("test-stream", ExpectedVersion.Any, TestEvent.NewTestEvent(), TestEvent.NewTestEvent());

            var res = _connection.ReadStreamEventsForward("test-stream", 0, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(5 + 1, res.Events.Length);
            for (int i = 0; i < 6; ++i)
            {
                Assert.AreEqual(i, res.Events[i].OriginalEventNumber);
            }
        }

        [Test]
        public void committing_first_event_with_expected_version_no_stream_is_idempotent()
        {
            _connection.AppendToStream("test-stream", ExpectedVersion.NoStream, _firstEvent);

            var res = _connection.ReadStreamEventsForward("test-stream", 0, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(3 + 1, res.Events.Length);
            for (int i = 0; i < 4; ++i)
            {
                Assert.AreEqual(i, res.Events[i].OriginalEventNumber);
            }
        }

        [Test]
        public void trying_to_append_new_events_with_expected_version_no_stream_fails()
        {
            Assert.That(() => _connection.AppendToStream("test-stream", ExpectedVersion.NoStream, TestEvent.NewTestEvent()),
                        Throws.Exception.InstanceOf<AggregateException>()
                        .With.InnerException.InstanceOf<WrongExpectedVersionException>());
        }
    }
}
