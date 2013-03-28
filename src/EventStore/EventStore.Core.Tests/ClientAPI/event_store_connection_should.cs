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
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helper;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("LongRunning")]
    public class event_store_connection_should: SpecificationWithDirectoryPerTestFixture
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

        [Test]
        [Category("Network")]
        public void not_throw_on_close_if_connect_was_not_called()
        {
            var connection = TestConnection.Create();
            Assert.DoesNotThrow(connection.Close);
        }

        [Test]
        [Category("Network")]
        public void not_throw_on_close_if_called_multiple_times()
        {
            var connection = TestConnection.Create();
            connection.Connect(_node.TcpEndPoint);
            connection.Close();
            Assert.DoesNotThrow(connection.Close);
        }

        [Test]
        [Category("Network")]
        public void throw_on_connect_called_more_than_once()
        {
            var connection = TestConnection.Create();
            Assert.DoesNotThrow(() => connection.Connect(_node.TcpEndPoint));

            Assert.That(() => connection.Connect(_node.TcpEndPoint),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());
        }

        [Test]
        [Category("Network")]
        public void throw_on_connect_called_after_close()
        {
            var connection = TestConnection.Create();
            connection.Connect(_node.TcpEndPoint);
            connection.Close();

            Assert.That(() => connection.Connect(_node.TcpEndPoint),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());
        }

        [Test]
        [Category("Network")]
        public void throw_invalid_operation_on_every_api_call_if_connect_was_not_called()
        {
            var connection = TestConnection.Create();

            const string s = "stream";
            var events = new[] { TestEvent.NewTestEvent() };
            var bytes = new byte[0];

            Assert.That(() => connection.DeleteStream(s, 0),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());
            Assert.That(() => connection.DeleteStreamAsync(s, 0).Wait(),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());

            Assert.That(() => connection.AppendToStream(s, 0, events),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());
            Assert.That(() => connection.AppendToStreamAsync(s, 0, events).Wait(),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());

            Assert.That(() => connection.ReadStreamEventsForward(s, 0, 1, false),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());
            Assert.That(() => connection.ReadStreamEventsForwardAsync(s, 0, 1, resolveLinkTos: false).Wait(),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());

            Assert.That(() => connection.ReadStreamEventsBackward(s, 0, 1, false),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());
            Assert.That(() => connection.ReadStreamEventsBackwardAsync(s, 0, 1, resolveLinkTos: false).Wait(),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());

            Assert.That(() => connection.ReadAllEventsForward(Position.Start, 1, false),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());
            Assert.That(() => connection.ReadAllEventsForwardAsync(Position.Start, 1, false).Wait(),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());

            Assert.That(() => connection.ReadAllEventsBackward(Position.End, 1, false),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());
            Assert.That(() => connection.ReadAllEventsBackwardAsync(Position.End, 1, false).Wait(),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());

            Assert.That(() => connection.StartTransaction(s, 0),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());
            Assert.That(() => connection.StartTransactionAsync(s, 0).Wait(),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());

            Assert.That(() => connection.SubscribeToStream(s, false, _ => { }, () => { }).Wait(),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());

            Assert.That(() => connection.SubscribeToAll(false, _ => { }, () => { }).Wait(),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());
        }
    }
}
