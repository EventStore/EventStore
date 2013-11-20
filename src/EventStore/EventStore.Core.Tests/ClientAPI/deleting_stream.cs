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
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("LongRunning")]
    public class deleting_stream : SpecificationWithDirectoryPerTestFixture
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
        public void which_doesnt_exists_should_success_when_passed_empty_stream_expected_version()
        {
            const string stream = "which_already_exists_should_success_when_passed_empty_stream_expected_version";
            using (var connection = TestConnection.Create(_node.TcpEndPoint))
            {
                connection.Connect();
                var delete = connection.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream, hardDelete: true);
                Assert.DoesNotThrow(delete.Wait);
            }
        }

        [Test]
        [Category("Network")]
        public void which_doesnt_exists_should_success_when_passed_any_for_expected_version()
        {
            const string stream = "which_already_exists_should_success_when_passed_any_for_expected_version";
            using (var connection = TestConnection.Create(_node.TcpEndPoint))
            {
                connection.Connect();

                var delete = connection.DeleteStreamAsync(stream, ExpectedVersion.Any, hardDelete: true);
                Assert.DoesNotThrow(delete.Wait);
            }
        }

        [Test]
        [Category("Network")]
        public void with_invalid_expected_version_should_fail()
        {
            const string stream = "with_invalid_expected_version_should_fail";
            using (var connection = TestConnection.Create(_node.TcpEndPoint))
            {
                connection.Connect();

                var delete = connection.DeleteStreamAsync(stream, 1, hardDelete: true);
                Assert.That(() => delete.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<WrongExpectedVersionException>());
            }
        }

        [Test]
        [Category("Network")]
        public void which_was_already_deleted_should_fail()
        {
            const string stream = "which_was_allready_deleted_should_fail";
            using (var connection = TestConnection.Create(_node.TcpEndPoint))
            {
                connection.Connect();

                var delete = connection.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream, hardDelete: true);
                Assert.DoesNotThrow(delete.Wait);

                var secondDelete = connection.DeleteStreamAsync(stream, ExpectedVersion.Any, hardDelete: true);
                Assert.That(() => secondDelete.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<StreamDeletedException>());
            }
        }
    }
}
