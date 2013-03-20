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
using EventStore.ClientAPI.Exceptions;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helper;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("LongRunning")]
    public class creating_stream: SpecificationWithDirectoryPerTestFixture
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
        public void which_does_not_exist_should_be_successfull()
        {
            const string stream = "which_does_not_exist_should_be_successfull";
            using (var connection = TestConnection.Create())
            {
                connection.Connect(_node.TcpEndPoint);
                var create = connection.CreateStreamAsync(stream, Guid.NewGuid(), false, new byte[0]);
                Assert.DoesNotThrow(create.Wait);
            }
        }

        [Test]
        [Category("Network")]
        public void many_times_with_same_id_should_succeed()
        {
            const string stream = "many_times_with_same_id_should_succeed";
            using (var connection = TestConnection.Create())
            {
                connection.Connect(_node.TcpEndPoint);
                var id = Guid.NewGuid();

                var create1 = connection.CreateStreamAsync(stream, id, false, new byte[0]);
                Assert.DoesNotThrow(create1.Wait);

                var create2 = connection.CreateStreamAsync(stream, id, false, new byte[0]);
                Assert.DoesNotThrow(create2.Wait);

                var read = connection.ReadStreamEventsForwardAsync(stream, 0, 10, resolveLinkTos: false);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(read.Result.Events.Length, Is.EqualTo(1));
            }
        }

        [Test]
        [Category("Network")]
        public void which_supposed_to_be_system_should_succees__but_on_your_own_risk()
        {
            const string stream = "$which_supposed_to_be_system_should_succees__but_on_your_own_risk";
            using (var connection = TestConnection.Create())
            {
                connection.Connect(_node.TcpEndPoint);
                var create = connection.CreateStreamAsync(stream, Guid.NewGuid(), false, new byte[0]);
                Assert.DoesNotThrow(create.Wait);
            }
        }

        [Test]
        [Category("Network")]
        public void which_already_exists_should_fail()
        {
            const string stream = "which_already_exists_should_fail";
            using (var connection = TestConnection.Create())
            {
                connection.Connect(_node.TcpEndPoint);
                var initialCreate = connection.CreateStreamAsync(stream, Guid.NewGuid(), false, new byte[0]);
                Assert.DoesNotThrow(initialCreate.Wait);

                var secondCreate = connection.CreateStreamAsync(stream, Guid.NewGuid(), false, new byte[0]);
                Assert.That(() => secondCreate.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<WrongExpectedVersionException>());
            }
        }

        [Test]
        [Category("Network")]
        public void which_was_deleted_should_fail()
        {
            const string stream = "which_was_deleted_should_fail";
            using (var connection = TestConnection.Create())
            {
                connection.Connect(_node.TcpEndPoint);
                var create = connection.CreateStreamAsync(stream, Guid.NewGuid(), false, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var delete = connection.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream);
                Assert.DoesNotThrow(delete.Wait);

                var secondCreate = connection.CreateStreamAsync(stream, Guid.NewGuid(), false, new byte[0]);
                Assert.That(() => secondCreate.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<StreamDeletedException>());
            }
        }
    }
}