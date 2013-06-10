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
using System.Threading;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Core.Services.UserManagement;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Security
{
    public abstract class AuthenticationTestBase : SpecificationWithDirectoryPerTestFixture
    {
        private readonly UserCredentials _userCredentials;
        private MiniNode _node;
        protected IEventStoreConnection Connection;

        protected AuthenticationTestBase(UserCredentials userCredentials = null)
        {
            _userCredentials = userCredentials;
        }

        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
            _node = new MiniNode(PathName);
            _node.Start();

            var userCreateEvent1 = new ManualResetEventSlim();
            _node.Node.MainQueue.Publish(
                new UserManagementMessage.Create(
                    new CallbackEnvelope(
                        m =>
                            {
                                Assert.IsTrue(m is UserManagementMessage.UpdateResult);
                                var msg = (UserManagementMessage.UpdateResult) m;
                                Assert.IsTrue(msg.Success);

                                userCreateEvent1.Set();
                            }), SystemAccount.Principal, "user1", "Test User 1", new string[0], "pa$$1"));

            var userCreateEvent2 = new ManualResetEventSlim();
            _node.Node.MainQueue.Publish(
                new UserManagementMessage.Create(
                    new CallbackEnvelope(
                        m =>
                            {
                                Assert.IsTrue(m is UserManagementMessage.UpdateResult);
                                var msg = (UserManagementMessage.UpdateResult) m;
                                Assert.IsTrue(msg.Success);

                                userCreateEvent2.Set();
                            }), SystemAccount.Principal, "user2", "Test User 2", new string[0], "pa$$2"));

            var adminCreateEvent2 = new ManualResetEventSlim();
            _node.Node.MainQueue.Publish(
                new UserManagementMessage.Create(
                    new CallbackEnvelope(
                        m =>
                        {
                            Assert.IsTrue(m is UserManagementMessage.UpdateResult);
                            var msg = (UserManagementMessage.UpdateResult)m;
                            Assert.IsTrue(msg.Success);

                            adminCreateEvent2.Set();
                        }), SystemAccount.Principal, "adm", "Administrator User", new[] { SystemUserGroups.Admins}, "admpa$$"));

            Assert.IsTrue(userCreateEvent1.Wait(120000), "User 1 creation failed");
            Assert.IsTrue(userCreateEvent2.Wait(120000), "User 2 creation failed");
            Assert.IsTrue(adminCreateEvent2.Wait(120000), "Administrator User creation failed");

            Connection = TestConnection.Create(_node.TcpEndPoint, TcpType.Normal, _userCredentials);
            Connection.Connect();

            Connection.SetStreamMetadata("noacl-stream", ExpectedVersion.NoStream, Guid.NewGuid(),
                                         StreamMetadata.Build());
            Connection.SetStreamMetadata("read-stream", ExpectedVersion.NoStream, Guid.NewGuid(), 
                                         StreamMetadata.Build().SetReadRole("user1"));
            Connection.SetStreamMetadata("write-stream", ExpectedVersion.NoStream, Guid.NewGuid(),
                                         StreamMetadata.Build().SetWriteRole("user1"));
            Connection.SetStreamMetadata("metaread-stream", ExpectedVersion.NoStream, Guid.NewGuid(),
                                         StreamMetadata.Build().SetMetadataReadRole("user1"));
            Connection.SetStreamMetadata("metawrite-stream", ExpectedVersion.NoStream, Guid.NewGuid(),
                                         StreamMetadata.Build().SetMetadataWriteRole("user1"));

            Connection.SetStreamMetadata("$all", ExpectedVersion.Any, Guid.NewGuid(), 
                                         StreamMetadata.Build().SetReadRole("user1"), new UserCredentials("adm", "admpa$$"));

            Connection.SetStreamMetadata("$system-acl", ExpectedVersion.NoStream, Guid.NewGuid(),
                                         StreamMetadata.Build()
                                                       .SetReadRole("user1")
                                                       .SetWriteRole("user1")
                                                       .SetMetadataReadRole("user1")
                                                       .SetMetadataWriteRole("user1"), new UserCredentials("adm", "admpa$$"));
            Connection.SetStreamMetadata("$system-adm", ExpectedVersion.NoStream, Guid.NewGuid(),
                                         StreamMetadata.Build()
                                                       .SetReadRole(SystemUserGroups.Admins)
                                                       .SetWriteRole(SystemUserGroups.Admins)
                                                       .SetMetadataReadRole(SystemUserGroups.Admins)
                                                       .SetMetadataWriteRole(SystemUserGroups.Admins), new UserCredentials("adm", "admpa$$"));

            Connection.SetStreamMetadata("normal-all", ExpectedVersion.NoStream, Guid.NewGuid(),
                                         StreamMetadata.Build()
                                                       .SetReadRole(SystemUserGroups.All)
                                                       .SetWriteRole(SystemUserGroups.All)
                                                       .SetMetadataReadRole(SystemUserGroups.All)
                                                       .SetMetadataWriteRole(SystemUserGroups.All));
            Connection.SetStreamMetadata("$system-all", ExpectedVersion.NoStream, Guid.NewGuid(),
                                         StreamMetadata.Build()
                                                       .SetReadRole(SystemUserGroups.All)
                                                       .SetWriteRole(SystemUserGroups.All)
                                                       .SetMetadataReadRole(SystemUserGroups.All)
                                                       .SetMetadataWriteRole(SystemUserGroups.All), new UserCredentials("adm", "admpa$$"));
        }

        [TestFixtureTearDown]
        public override void TestFixtureTearDown()
        {
            _node.Shutdown();
            Connection.Close();
            base.TestFixtureTearDown();
        }

        protected void ReadStreamForward(string streamId, string login, string password)
        {
            Connection.ReadStreamEventsForward(streamId, 0, 1, false,
                                               login == null && password == null ? null : new UserCredentials(login, password));
        }

        protected void ReadStreamBackward(string streamId, string login, string password)
        {
            Connection.ReadStreamEventsBackward(streamId, 0, 1, false,
                                                login == null && password == null ? null : new UserCredentials(login, password));
        }

        protected void WriteStream(string streamId, string login, string password)
        {
            Connection.AppendToStream(streamId, ExpectedVersion.Any, CreateEvents(),
                                      login == null && password == null ? null : new UserCredentials(login, password));
        }

        protected EventStoreTransaction TransStart(string streamId, string login, string password)
        {
            return  Connection.StartTransaction(streamId, ExpectedVersion.Any,
                                                login == null && password == null ? null : new UserCredentials(login, password));
        }

        protected void ReadAllForward(string login, string password)
        {
            Connection.ReadAllEventsForward(Position.Start, 1, false,
                                            login == null && password == null ? null : new UserCredentials(login, password));
        }

        protected void ReadAllBackward(string login, string password)
        {
            Connection.ReadAllEventsBackward(Position.End, 1, false,
                                             login == null && password == null ? null : new UserCredentials(login, password));
        }

        protected void ReadMeta(string streamId, string login, string password)
        {
            Connection.GetStreamMetadataAsRawBytes(streamId, login == null && password == null ? null : new UserCredentials(login, password));
        }

        protected void WriteMeta(string streamId, string login, string password, string metawriteRole)
        {
            Connection.SetStreamMetadata(streamId,
                                         ExpectedVersion.Any,
                                         Guid.NewGuid(),
                                         metawriteRole == null
                                            ? StreamMetadata.Build()
                                            : StreamMetadata.Build().SetReadRole(metawriteRole)
                                                                    .SetWriteRole(metawriteRole)
                                                                    .SetMetadataReadRole(metawriteRole)
                                                                    .SetMetadataWriteRole(metawriteRole),
                                         login == null && password == null ? null : new UserCredentials(login, password));
        }

        protected void SubscribeToStream(string streamId, string login, string password)
        {
            Connection.SubscribeToStream(streamId, false, (x, y) => { }, (x, y, z) => { },
                                         login == null && password == null ? null : new UserCredentials(login, password));
        }

        protected void SubscribeToAll(string login, string password)
        {
            Connection.SubscribeToAll(false, (x, y) => { }, (x, y, z) => { },
                                      login == null && password == null ? null : new UserCredentials(login, password));
        }

        protected void Expect<T>(Action action) where T : Exception
        {
            Assert.That(() => action(), Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<T>());
        }

        protected void ExpectNoException(Action action)
        {
            Assert.That(() => action(), Throws.Nothing);
        }

        protected EventData[] CreateEvents()
        {
            return new[] { new EventData(Guid.NewGuid(), "some-type", false, new byte[] {1, 2, 3}, null) };
        }
    }
}