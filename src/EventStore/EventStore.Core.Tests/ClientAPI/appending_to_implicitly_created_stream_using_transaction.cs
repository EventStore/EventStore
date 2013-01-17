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
using EventStore.ClientAPI.Exceptions;
using EventStore.Core.Tests.ClientAPI.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("LongRunning")]
    internal class appending_to_implicitly_created_stream_using_transaction: SpecificationWithDirectoryPerTestFixture
    {
        private MiniNode _node;

        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
            _node = new MiniNode(PathName);
            _node.Start();
        }

        [TestFixtureTearDown]
        public override void TestFixtureTearDown()
        {
            _node.Shutdown();
            base.TestFixtureTearDown();
        }

        /*
         * sequence - events written so stream
         * 1em1 - event number 1 written with exp version -1 (minus 1)
         * 1any - event number 1 written with exp version any
         * S_1em1_2em1_E - START bucket, two events in bucket, END bucket
        */

        [Test]
        [Category("Network")]
        public void sequence_1em1_2e1_3e2_4e3_5e4_6e5_1em1_idempotent()
        {
            const string stream = "appending_to_implicitly_created_stream_using_transaction_sequence_1em1_2e1_3e2_4e3_5e4_6e5_1em1_idempotent";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);

                var events = Enumerable.Range(1, 6).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
                var writer = new TransactionalWriter(store, stream);

                Assert.DoesNotThrow(() => writer.StartTransaction(-1).Write(events).Commit());
                Assert.DoesNotThrow(() => writer.StartTransaction(-1).Write(events.First()).Commit());

                var total = EventsStream.Count(store, stream);
                Assert.That(total, Is.EqualTo(events.Length + 1));
            }
        }

        [Test]
        [Category("Network")]
        public void sequence_1em1_2e1_3e2_4e3_5e4_6e5_1any_idempotent()
        {
            const string stream = "appending_to_implicitly_created_stream_using_transaction_sequence_1em1_2e1_3e2_4e3_5e4_6e5_1any_idempotent";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);

                var events = Enumerable.Range(1, 6).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
                var writer = new TransactionalWriter(store, stream);

                Assert.DoesNotThrow(() => writer.StartTransaction(-1).Write(events).Commit());
                Assert.DoesNotThrow(() => writer.StartTransaction(ExpectedVersion.Any).Write(events.First()).Commit());

                var total = EventsStream.Count(store, stream);
                Assert.That(total, Is.EqualTo(events.Length + 1));
            }
        }

        [Test]
        [Category("Network")]
        public void sequence_1em1_2e1_3e2_4e3_5e4_6e5_1e6_non_idempotent()
        {
            const string stream = "appending_to_implicitly_created_stream_using_transaction_sequence_1em1_2e1_3e2_4e3_5e4_6e5_1e6_non_idempotent";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);

                var events = Enumerable.Range(1, 6).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
                var writer = new TransactionalWriter(store, stream);

                Assert.DoesNotThrow(() => writer.StartTransaction(-1).Write(events).Commit());
                Assert.DoesNotThrow(() => writer.StartTransaction(6).Write(events.First()).Commit());

                var total = EventsStream.Count(store, stream);
                Assert.That(total, Is.EqualTo(events.Length + 1 + 1));
            }
        }

        [Test]
        [Category("Network")]
        public void sequence_1em1_2e1_3e2_4e3_5e4_6e5_1e7_wev()
        {
            const string stream = "appending_to_implicitly_created_stream_using_transaction_sequence_1em1_2e1_3e2_4e3_5e4_6e5_1e7_wev";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);

                var events = Enumerable.Range(1, 6).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
                var writer = new TransactionalWriter(store, stream);

                Assert.DoesNotThrow(() => writer.StartTransaction(-1).Write(events).Commit());
                Assert.That(() => writer.StartTransaction(7).Write(events.First()).Commit(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<WrongExpectedVersionException>());
            }
        }

        [Test]
        [Category("Network")]
        public void sequence_1em1_2e1_3e2_4e3_5e4_6e5_1e5_wev()
        {
            const string stream = "appending_to_implicitly_created_stream_using_transaction_sequence_1em1_2e1_3e2_4e3_5e4_6e5_1e5_wev";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);

                var events = Enumerable.Range(1, 6).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
                var writer = new TransactionalWriter(store, stream);

                Assert.DoesNotThrow(() => writer.StartTransaction(-1).Write(events).Commit());
                Assert.That(() => writer.StartTransaction(5).Write(events.First()).Commit(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<WrongExpectedVersionException>());
            }
        }

        [Test]
        [Category("Network")]
        public void sequence_1em1_1e1_non_idempotent()
        {
            const string stream = "appending_to_implicitly_created_stream_using_transaction_sequence_1em1_1e1_non_idempotent";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);

                var events = Enumerable.Range(1, 1).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
                var writer = new TransactionalWriter(store, stream);

                Assert.DoesNotThrow(() => writer.StartTransaction(-1).Write(events).Commit());
                Assert.DoesNotThrow(() => writer.StartTransaction(1).Write(events.First()).Commit());

                var total = EventsStream.Count(store, stream);
                Assert.That(total, Is.EqualTo(events.Length + 1 + 1));
            }
        }

        [Test]
        [Category("Network")]
        public void sequence_1em1_1any_idempotent()
        {
            const string stream = "appending_to_implicitly_created_stream_using_transaction_sequence_1em1_1any_idempotent";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);

                var events = Enumerable.Range(1, 1).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
                var writer = new TransactionalWriter(store, stream);

                Assert.DoesNotThrow(() => writer.StartTransaction(-1).Write(events).Commit());
                Assert.DoesNotThrow(() => writer.StartTransaction(ExpectedVersion.Any).Write(events.First()).Commit());

                var total = EventsStream.Count(store, stream);
                Assert.That(total, Is.EqualTo(events.Length + 1));
            }
        }

        [Test]
        [Category("Network")]
        public void sequence_1em1_1em1_idempotent()
        {
            const string stream = "appending_to_implicitly_created_stream_using_transaction_sequence_1em1_1em1_idempotent";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);

                var events = Enumerable.Range(1, 1).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
                var writer = new TransactionalWriter(store, stream);

                Assert.DoesNotThrow(() => writer.StartTransaction(-1).Write(events).Commit());
                Assert.DoesNotThrow(() => writer.StartTransaction(-1).Write(events.First()).Commit());

                var total = EventsStream.Count(store, stream);
                Assert.That(total, Is.EqualTo(events.Length + 1));
            }
        }

        [Test]
        [Category("Network")]
        public void sequence_1em1_2e1_3e2_2any_2any_idempotent()
        {
            const string stream = "appending_to_implicitly_created_stream_using_transaction_sequence_1em1_2e1_3e2_2any_2any_idempotent";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);

                var events = Enumerable.Range(1, 3).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
                var writer = new TransactionalWriter(store, stream);

                Assert.DoesNotThrow(() => writer.StartTransaction(-1).Write(events).Commit());
                Assert.DoesNotThrow(() => writer.StartTransaction(ExpectedVersion.Any).Write(events[1]).Write(events[1]).Commit());

                var total = EventsStream.Count(store, stream);
                Assert.That(total, Is.EqualTo(events.Length + 1));
            }
        }

        [Test]
        [Category("Network")]
        public void sequence_S_1em1_2em1_E_S_1em1_2em1_3em1_E_idempotancy_fail()
        {
            const string stream = "appending_to_implicitly_created_stream_using_transaction_sequence_S_1em1_2em1_E_S_1em1_2em1_3em1_E_idempotancy_fail";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);

                var events = Enumerable.Range(1, 2).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
                var writer = new TransactionalWriter(store, stream);

                Assert.DoesNotThrow(() => writer.StartTransaction(-1).Write(events).Commit());
                Assert.That(() => writer.StartTransaction(-1).Write(events.Concat(new[] { TestEvent.NewTestEvent(Guid.NewGuid()) }).ToArray()).Commit(),
                            Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<WrongExpectedVersionException>());
            }
        }
    }
}
