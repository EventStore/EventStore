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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using EventStore.ClientAPI.Data;
using NUnit.Framework;

namespace EventStore.ClientAPI
{
    [TestFixture, Explicit]
    public class usage_shows_how
    {
        public class TestEvent : Event
        {
            public Guid EventId { get; private set; }
            public string Type { get; private set; }
            public byte[] Data { get; private set; }
            public byte[] Metadata { get; private set; }

            public TestEvent(string data)
            {
                EventId = Guid.NewGuid();
                Type = GetType().FullName;
                Data = Encoding.UTF8.GetBytes(data);
                Metadata = new byte[0];
            }
        }

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            EventStore.Configure(Configure.AsDefault());
        }

        [Test]
        public void create_stream()
        {
            string stream = "NewStream-" + Guid.NewGuid();
            EventStore.CreateStream(stream, new byte[] {1,2,3});
            EventStore.AppendToStream(stream, 0, new [] { new TestEvent("Some data") });
        }

        [Test]
        public void create_stream_once()
        {
            string stream = "NewStream-" + Guid.NewGuid();
            EventStore.CreateStream(stream, new byte[] { 1, 2, 3 });

            try
            {
                EventStore.CreateStream(stream, new byte[] { 3, 3, 3 });
            }
            catch (AggregateException aggregateException)
            {
                Assert.That(aggregateException.InnerExceptions[0]
                            .Message.Contains("WrongExpectedVersion"));
            }
            EventStore.AppendToStream(stream, 0, new[] { new TestEvent("Some data") });
        }

        [Test]
        public void create_stream_with_protobuf()
        {
            string stream = "NewStream-protobuf-" + Guid.NewGuid();
            var metadata = new Dictionary<string, string>() { { "fied1", "value1" }, { "field2", "value2" } };
            EventStore.CreateStreamWithProtoBufMetadata(stream, metadata);
        }

        [Test]
        public void write_to()
        {
            var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1113);
            using (var connection = new EventStoreConnection(endpoint))
            {
                var ev = new TestEvent("Some data");
                var task = connection.AppendToStreamAsync("test", -2, new[] { ev });
                task.Wait();

                var result = task.Result;
                Assert.IsTrue(result.IsSuccessful, "Written Successfully");
            }
        }

        [Test]
        public void write_to_sync()
        {
            var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1113);
            using (var connection = new EventStoreConnection(endpoint))
            {
                var ev = new TestEvent("Some data");
                connection.AppendToStream("test", -2, new[] { ev });
            }
        }

        [Test]
        public void read_from()
        {
            write_to();
            write_to();

            var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1113);
            using (var connection = new EventStoreConnection(endpoint))
            {
                var task = connection.ReadEventStreamAsync("test", 0, 2);
                task.Wait();

                var result = task.Result;
                Assert.AreEqual(result.Events.Length, 2);
            }
        }

        //[Test]
        //public void write_sync_null_failure()
        //{
        //    var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1113);
        //    using (var connection = new EventStoreConnection(endpoint))
        //    {
        //        var ev = new TestEvent("Some data");
        //        var task = connection.AppendToStreamAsync("test", -2, new[] { ev });
        //        task.Wait();

        //        var result = task.Result;
        //        Assert.IsTrue(result.IsSuccessful, "Written Successfully");
        //    }
        //}

        [Test]
        public void write_to_and_delete() {
            var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1113);
            using (var connection = new EventStoreConnection(endpoint))
            {
                var ev = new TestEvent("Some data");
                var stream = string.Format("test-delete-{0}", Guid.NewGuid());

                var appendTask = connection.AppendToStreamAsync(stream, -2, new[] { ev });
                appendTask.Wait();
                Assert.IsTrue(appendTask.Result.IsSuccessful, "Failed to append data to stream.");

                var data = connection.ReadEventStream(stream, 0, int.MaxValue);

                var lastEventVersion = data.Events[data.Events.Length - 1].EventNumber;

                var deleteTask = connection.DeleteStreamAsync(stream, lastEventVersion);
                deleteTask.Wait();
                Assert.IsTrue(deleteTask.Result.IsSuccessful, "Failed to delete stream.");
            }
        }

        [Test]
        public void write_and_read_when_configured_as_default()
        {
            EventStore.Configure(Configure.AsDefault());

            var ev = new TestEvent("Some data");

            string stream = "stream-configure_on_default-" + Guid.NewGuid();
            EventStore.AppendToStream(stream, ExpectedVersion.Any, new[] { ev });

            var events = EventStore.ReadEventStream(stream, 0, 5);

            Assert.AreEqual(events.Events[1].EventId, ev.EventId);
        }


        [Test]
        public void write_read_and_delete_with_version_when_configured_as_default()
        {
            EventStore.Configure(Configure.AsDefault());

            var ev = new TestEvent("Some data");
            string stream = "stream-configure_on_default-" + Guid.NewGuid();
            EventStore.AppendToStream(stream, ExpectedVersion.Any, new[] { ev });

            var events = EventStore.ReadEventStream(stream, 0, 5);
            Assert.AreEqual(events.Events[1].EventId, ev.EventId);

            EventStore.DeleteStream(stream, 1);
        }

        [Test]
        public void delete_not_existing_stream()
        {
            EventStore.Configure(Configure.AsDefault());
            EventStore.DeleteStream(Guid.NewGuid().ToString());
        }

        [Test]
        public void write_read_and_delete_when_configured_as_default()
        {
            EventStore.Configure(Configure.AsDefault());

            var ev = new TestEvent("Some data");
            string stream = "stream-configure_on_default-" + Guid.NewGuid();
            EventStore.AppendToStream(stream, ExpectedVersion.Any, new[] { ev });
            
            var events = EventStore.ReadEventStream(stream, 0, 5);
            Assert.AreEqual(events.Events[1].EventId, ev.EventId);

            EventStore.DeleteStream(stream);
        }

        [Test]
        public void write_to_loop()
        {
            var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1113);
            using (var connection = new EventStoreConnection(endpoint))
            {
                for (var i = 0; i < 1000; ++i)
                {
                    var ev = new TestEvent("Some data");
                    connection.AppendToStream("test", -2, new[] { ev });
                }
            }
        }

        [Test]
        public void write_to_loop_async()
        {
            var stopwatch = new Stopwatch();
            var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1113);
            using (var connection = new EventStoreConnection(endpoint))
            {
                const int itemsCount = 1000;

                stopwatch.Start();
                var tasks = new Task[itemsCount];
                for (var i = 0; i < itemsCount; ++i)
                {
                    var ev = new TestEvent("Some data async");
                    tasks[i] = connection.AppendToStreamAsync("test", -2, new[] { ev });
                }
                Task.WaitAll(tasks);

                stopwatch.Stop();

                Debug.WriteLine("Completed {0} event with speed {1} e/sec", 
                                itemsCount,
                                itemsCount / stopwatch.Elapsed.TotalSeconds);
            }
        }
    }
}
;