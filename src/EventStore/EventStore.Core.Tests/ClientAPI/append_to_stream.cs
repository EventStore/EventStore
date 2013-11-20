﻿// Copyright (c) 2012, Event Store LLP
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
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("LongRunning")]
    public class append_to_stream : SpecificationWithDirectoryPerTestFixture
    {
        private readonly TcpType _tcpType = TcpType.Normal;
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

        [Test, Category("LongRunning"), Category("Network")]
        public void should_allow_appending_zero_events_to_stream_with_no_problems()
        {
            const string stream1 = "should_allow_appending_zero_events_to_stream_with_no_problems1";
            const string stream2 = "should_allow_appending_zero_events_to_stream_with_no_problems2";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.Connect();

                Assert.AreEqual(-1, store.AppendToStream(stream1, ExpectedVersion.Any).NextExpectedVersion);
                Assert.AreEqual(-1, store.AppendToStream(stream1, ExpectedVersion.NoStream).NextExpectedVersion);
                Assert.AreEqual(-1, store.AppendToStream(stream1, ExpectedVersion.Any).NextExpectedVersion);
                Assert.AreEqual(-1, store.AppendToStream(stream1, ExpectedVersion.NoStream).NextExpectedVersion);

                var read1 = store.ReadStreamEventsForward(stream1, 0, 2, resolveLinkTos: false);
                Assert.That(read1.Events.Length, Is.EqualTo(0));

                Assert.AreEqual(-1, store.AppendToStream(stream2, ExpectedVersion.NoStream).NextExpectedVersion);
                Assert.AreEqual(-1, store.AppendToStream(stream2, ExpectedVersion.Any).NextExpectedVersion);
                Assert.AreEqual(-1, store.AppendToStream(stream2, ExpectedVersion.NoStream).NextExpectedVersion);
                Assert.AreEqual(-1, store.AppendToStream(stream2, ExpectedVersion.Any).NextExpectedVersion);

                var read2 = store.ReadStreamEventsForward(stream2, 0, 2, resolveLinkTos: false);
                Assert.That(read2.Events.Length, Is.EqualTo(0));
            }
        }
        
        [Test, Category("LongRunning"), Category("Network")]
        public void should_create_stream_with_no_stream_exp_ver_on_first_write_if_does_not_exist()
        {
            const string stream = "should_create_stream_with_no_stream_exp_ver_on_first_write_if_does_not_exist";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.Connect();

                Assert.AreEqual(0, store.AppendToStream(stream, ExpectedVersion.NoStream, TestEvent.NewTestEvent()).NextExpectedVersion);

                var read = store.ReadStreamEventsForwardAsync(stream, 0, 2, resolveLinkTos: false);
                Assert.DoesNotThrow(read.Wait);
                Assert.That(read.Result.Events.Length, Is.EqualTo(1));
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void should_create_stream_with_any_exp_ver_on_first_write_if_does_not_exist()
        {
            const string stream = "should_create_stream_with_any_exp_ver_on_first_write_if_does_not_exist";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.Connect();
                Assert.AreEqual(0, store.AppendToStream(stream, ExpectedVersion.Any, TestEvent.NewTestEvent()).NextExpectedVersion);

                var read = store.ReadStreamEventsForwardAsync(stream, 0, 2, resolveLinkTos: false);
                Assert.DoesNotThrow(read.Wait);
                Assert.That(read.Result.Events.Length, Is.EqualTo(1));
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void should_fail_writing_with_correct_exp_ver_to_deleted_stream()
        {
            const string stream = "should_fail_writing_with_correct_exp_ver_to_deleted_stream";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.Connect();

                var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream, hardDelete: true);
                Assert.DoesNotThrow(delete.Wait);

                var append = store.AppendToStreamAsync(stream, ExpectedVersion.NoStream, new[] { TestEvent.NewTestEvent() });
                Assert.That(() => append.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<StreamDeletedException>());
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void should_fail_writing_with_any_exp_ver_to_deleted_stream()
        {
            const string stream = "should_fail_writing_with_any_exp_ver_to_deleted_stream";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.Connect();

                try
                {
                    store.DeleteStream(stream, ExpectedVersion.EmptyStream, hardDelete: true);
                }
                catch (Exception exc)
                {
                    Console.WriteLine(exc);
                    Assert.Fail();
                }

                var append = store.AppendToStreamAsync(stream, ExpectedVersion.Any, new[] { TestEvent.NewTestEvent() });
                Assert.That(() => append.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<StreamDeletedException>());
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void should_fail_writing_with_invalid_exp_ver_to_deleted_stream()
        {
            const string stream = "should_fail_writing_with_invalid_exp_ver_to_deleted_stream";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.Connect();

                var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream, hardDelete: true);
                Assert.DoesNotThrow(delete.Wait);

                var append = store.AppendToStreamAsync(stream, 5, new[] { TestEvent.NewTestEvent() });
                Assert.That(() => append.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<StreamDeletedException>());
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void should_append_with_correct_exp_ver_to_existing_stream()
        {
            const string stream = "should_append_with_correct_exp_ver_to_existing_stream";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.Connect();
                store.AppendToStream(stream, ExpectedVersion.EmptyStream, TestEvent.NewTestEvent());

                var append = store.AppendToStreamAsync(stream, 0, new[] { TestEvent.NewTestEvent() });
                Assert.DoesNotThrow(append.Wait);
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void should_append_with_any_exp_ver_to_existing_stream()
        {
            const string stream = "should_append_with_any_exp_ver_to_existing_stream";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.Connect();
                Assert.AreEqual(0, store.AppendToStream(stream, ExpectedVersion.EmptyStream, TestEvent.NewTestEvent()).NextExpectedVersion);
                Assert.AreEqual(1, store.AppendToStream(stream, ExpectedVersion.Any, TestEvent.NewTestEvent()).NextExpectedVersion);
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void should_fail_appending_with_wrong_exp_ver_to_existing_stream()
        {
            const string stream = "should_fail_appending_with_wrong_exp_ver_to_existing_stream";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.Connect();
                Assert.AreEqual(0, store.AppendToStream(stream, ExpectedVersion.EmptyStream, TestEvent.NewTestEvent()).NextExpectedVersion);

                var append = store.AppendToStreamAsync(stream, 1, new[] { TestEvent.NewTestEvent() });
                Assert.That(() => append.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<WrongExpectedVersionException>());
            }
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void can_append_multiple_events_at_once()
        {
            const string stream = "can_append_multiple_events_at_once";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.Connect();

                var events = Enumerable.Range(0, 100).Select(i => TestEvent.NewTestEvent(i.ToString(), i.ToString()));
                Assert.AreEqual(99, store.AppendToStream(stream, ExpectedVersion.EmptyStream, events).NextExpectedVersion);
            }
        }
    }

    [TestFixture, Category("LongRunning")]
    public class ssl_append_to_stream : SpecificationWithDirectoryPerTestFixture
    {
        private readonly TcpType _tcpType = TcpType.Ssl;
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

        [Test, Category("LongRunning"), Category("Network")]
        public void should_allow_appending_zero_events_to_stream_with_no_problems()
        {
            const string stream = "should_allow_appending_zero_events_to_stream_with_no_problems";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.Connect();
                Assert.AreEqual(-1, store.AppendToStream(stream, ExpectedVersion.NoStream).NextExpectedVersion);

                var read = store.ReadStreamEventsForward(stream, 0, 2, resolveLinkTos: false);
                Assert.That(read.Events.Length, Is.EqualTo(0));
            }
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void should_create_stream_with_no_stream_exp_ver_on_first_write_if_does_not_exist()
        {
            const string stream = "should_create_stream_with_no_stream_exp_ver_on_first_write_if_does_not_exist";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.Connect();
                Assert.AreEqual(0, store.AppendToStream(stream, ExpectedVersion.NoStream, TestEvent.NewTestEvent()).NextExpectedVersion);

                var read = store.ReadStreamEventsForwardAsync(stream, 0, 2, resolveLinkTos: false);
                Assert.DoesNotThrow(read.Wait);
                Assert.That(read.Result.Events.Length, Is.EqualTo(1));
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void should_create_stream_with_any_exp_ver_on_first_write_if_does_not_exist()
        {
            const string stream = "should_create_stream_with_any_exp_ver_on_first_write_if_does_not_exist";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.Connect();
                Assert.AreEqual(0, store.AppendToStream(stream, ExpectedVersion.Any, TestEvent.NewTestEvent()).NextExpectedVersion);

                var read = store.ReadStreamEventsForwardAsync(stream, 0, 2, resolveLinkTos: false);
                Assert.DoesNotThrow(read.Wait);
                Assert.That(read.Result.Events.Length, Is.EqualTo(1));
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void should_fail_writing_with_correct_exp_ver_to_deleted_stream()
        {
            const string stream = "should_fail_writing_with_correct_exp_ver_to_deleted_stream";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.Connect();

                var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream, hardDelete: true);
                Assert.DoesNotThrow(delete.Wait);

                var append = store.AppendToStreamAsync(stream, ExpectedVersion.NoStream, new[] { TestEvent.NewTestEvent() });
                Assert.That(() => append.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<StreamDeletedException>());
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void should_fail_writing_with_any_exp_ver_to_deleted_stream()
        {
            const string stream = "should_fail_writing_with_any_exp_ver_to_deleted_stream";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.Connect();

                var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream, hardDelete: true);
                Assert.DoesNotThrow(delete.Wait);

                var append = store.AppendToStreamAsync(stream, ExpectedVersion.Any, new[] { TestEvent.NewTestEvent() });
                Assert.That(() => append.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<StreamDeletedException>());
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void should_fail_writing_with_invalid_exp_ver_to_deleted_stream()
        {
            const string stream = "should_fail_writing_with_invalid_exp_ver_to_deleted_stream";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.Connect();

                var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream, hardDelete: true);
                Assert.DoesNotThrow(delete.Wait);

                var append = store.AppendToStreamAsync(stream, 5, new[] { TestEvent.NewTestEvent() });
                Assert.That(() => append.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<StreamDeletedException>());
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void should_append_with_correct_exp_ver_to_existing_stream()
        {
            const string stream = "should_append_with_correct_exp_ver_to_existing_stream";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.Connect();
                Assert.AreEqual(0, store.AppendToStream(stream, ExpectedVersion.EmptyStream, TestEvent.NewTestEvent()).NextExpectedVersion);
                Assert.AreEqual(1, store.AppendToStream(stream, 0, TestEvent.NewTestEvent()).NextExpectedVersion);
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void should_append_with_any_exp_ver_to_existing_stream()
        {
            const string stream = "should_append_with_any_exp_ver_to_existing_stream";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.Connect();
                Assert.AreEqual(0, store.AppendToStream(stream, ExpectedVersion.EmptyStream, TestEvent.NewTestEvent()).NextExpectedVersion);
                Assert.AreEqual(1, store.AppendToStream(stream, ExpectedVersion.Any, TestEvent.NewTestEvent()).NextExpectedVersion);
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void should_fail_appending_with_wrong_exp_ver_to_existing_stream()
        {
            const string stream = "should_fail_appending_with_wrong_exp_ver_to_existing_stream";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.Connect();
                Assert.AreEqual(0, store.AppendToStream(stream, ExpectedVersion.EmptyStream, TestEvent.NewTestEvent()).NextExpectedVersion);

                var append = store.AppendToStreamAsync(stream, 1, new[] { TestEvent.NewTestEvent() });
                Assert.That(() => append.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<WrongExpectedVersionException>());
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void can_append_multiple_events_at_once()
        {
            const string stream = "can_append_multiple_events_at_once";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.Connect();

                var events = Enumerable.Range(0, 100).Select(i => TestEvent.NewTestEvent(i.ToString(), i.ToString()));
                Assert.AreEqual(99, store.AppendToStream(stream, ExpectedVersion.EmptyStream, events).NextExpectedVersion);
            }
        }
    }

}
