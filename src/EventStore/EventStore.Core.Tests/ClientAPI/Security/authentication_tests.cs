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
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Security
{
    [TestFixture, Category("LongRunning"), Category("Network")]
    public class read_stream_security: security_test_fixture
    {
        [Test, Category("LongRunning"), Category("Network")]
        public void reading_stream_with_not_existing_credentials_is_not_authenticated()
        {
            Expect<NotAuthenticatedException>(() => ReadStreamForward("read-stream", "badlogin", "badpass"));
            Expect<NotAuthenticatedException>(() => ReadStreamBackward("read-stream", "badlogin", "badpass"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void reading_stream_with_no_credentials_is_denied()
        {
            Expect<AccessDeniedException>(() => ReadStreamForward("read-stream", null, null));
            Expect<AccessDeniedException>(() => ReadStreamBackward("read-stream", null, null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void reading_stream_with_not_authorized_user_credentials_is_denied()
        {
            Expect<AccessDeniedException>(() => ReadStreamForward("read-stream", "user2", "pa$$2"));
            Expect<AccessDeniedException>(() => ReadStreamBackward("read-stream", "user2", "pa$$2"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void reading_stream_with_authorized_user_credentials_succeeds()
        {
            ExpectNoException(() => ReadStreamForward("read-stream", "user1", "pa$$1"));
            ExpectNoException(() => ReadStreamBackward("read-stream", "user1", "pa$$1"));
        }


        [Test, Category("LongRunning"), Category("Network")]
        public void reading_no_acl_stream_succeeds_when_no_credentials_are_passed()
        {
            ExpectNoException(() => ReadStreamForward("noacl-stream", null, null));
            ExpectNoException(() => ReadStreamBackward("noacl-stream", null, null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void reading_no_acl_stream_succeeds_when_any_existing_user_credentials_are_passed()
        {
            ExpectNoException(() => ReadStreamForward("noacl-stream", "user1", "pa$$1"));
            ExpectNoException(() => ReadStreamBackward("noacl-stream", "user1", "pa$$1"));
            ExpectNoException(() => ReadStreamForward("noacl-stream", "user2", "pa$$2"));
            ExpectNoException(() => ReadStreamBackward("noacl-stream", "user2", "pa$$2"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void reading_no_acl_stream_is_not_authenticated_when_not_existing_credentials_are_passed()
        {
            Expect<NotAuthenticatedException>(() => ReadStreamForward("noacl-stream", "badlogin", "badpass"));
            Expect<NotAuthenticatedException>(() => ReadStreamBackward("noacl-stream", "badlogin", "badpass"));
        }
    }

    [TestFixture, Category("LongRunning"), Category("Network")]
    public class read_all_security : security_test_fixture
    {
        [Test, Category("LongRunning"), Category("Network")]
        public void reading_all_with_not_existing_credentials_is_not_authenticated()
        {
            Expect<NotAuthenticatedException>(() => ReadAllForward("badlogin", "badpass"));
            Expect<NotAuthenticatedException>(() => ReadAllBackward("badlogin", "badpass"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void reading_all_with_no_credentials_is_denied()
        {
            Expect<AccessDeniedException>(() => ReadAllForward(null, null));
            Expect<AccessDeniedException>(() => ReadAllBackward(null, null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void reading_all_with_not_authorized_user_credentials_is_denied()
        {
            Expect<AccessDeniedException>(() => ReadAllForward("user2", "pa$$2"));
            Expect<AccessDeniedException>(() => ReadAllBackward("user2", "pa$$2"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void reading_all_with_authorized_user_credentials_succeeds()
        {
            ExpectNoException(() => ReadAllForward("user1", "pa$$1"));
            ExpectNoException(() => ReadAllBackward("user1", "pa$$1"));
        }
    }

    [TestFixture, Category("LongRunning"), Category("Network")]
    public class write_stream_security : security_test_fixture
    {
        [Test, Category("LongRunning"), Category("Network")]
        public void writing_with_not_existing_credentials_is_not_authenticated()
        {
            Expect<NotAuthenticatedException>(() => WriteStream("write-stream", "badlogin", "badpass"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void writing_to_stream_with_no_credentials_is_denied()
        {
            Expect<AccessDeniedException>(() => WriteStream("write-stream", null, null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void writing_to_stream_with_not_authorized_user_credentials_is_denied()
        {
            Expect<AccessDeniedException>(() => WriteStream("write-stream", "user2", "pa$$2"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void writing_to_stream_with_authorized_user_credentials_succeeds()
        {
            ExpectNoException(() => WriteStream("write-stream", "user1", "pa$$1"));
        }


        [Test, Category("LongRunning"), Category("Network")]
        public void writing_to_no_acl_stream_succeeds_when_no_credentials_are_passed()
        {
            ExpectNoException(() => WriteStream("noacl-stream", null, null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void writing_to_no_acl_stream_succeeds_when_any_existing_user_credentials_are_passed()
        {
            ExpectNoException(() => WriteStream("noacl-stream", "user1", "pa$$1"));
            ExpectNoException(() => WriteStream("noacl-stream", "user2", "pa$$2"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void writing_to_no_acl_stream_is_not_authenticated_when_not_existing_credentials_are_passed()
        {
            Expect<NotAuthenticatedException>(() => WriteStream("noacl-stream", "badlogin", "badpass"));
        }
    }

    [TestFixture, Category("LongRunning"), Category("Network"), Ignore("Transactional security is not implemented yet")]
    public class transactional_write_stream_security : security_test_fixture
    {
    }

    [TestFixture, Category("LongRunning"), Category("Network")]
    public class read_stream_meta_security : security_test_fixture
    {
        [Test, Category("LongRunning"), Category("Network")]
        public void reading_stream_meta_with_not_existing_credentials_is_not_authenticated()
        {
            Expect<NotAuthenticatedException>(() => ReadMeta("metaread-stream", "badlogin", "badpass"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void reading_stream_meta_with_no_credentials_is_denied()
        {
            Expect<AccessDeniedException>(() => ReadMeta("metaread-stream", null, null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void reading_stream_meta_with_not_authorized_user_credentials_is_denied()
        {
            Expect<AccessDeniedException>(() => ReadMeta("metaread-stream", "user2", "pa$$2"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void reading_stream_meta_with_authorized_user_credentials_succeeds()
        {
            ExpectNoException(() => ReadMeta("metaread-stream", "user1", "pa$$1"));
        }


        [Test, Category("LongRunning"), Category("Network")]
        public void reading_no_acl_stream_meta_succeeds_when_no_credentials_are_passed()
        {
            ExpectNoException(() => ReadMeta("noacl-stream", null, null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void reading_no_acl_stream_meta_succeeds_when_any_existing_user_credentials_are_passed()
        {
            ExpectNoException(() => ReadMeta("noacl-stream", "user1", "pa$$1"));
            ExpectNoException(() => ReadMeta("noacl-stream", "user2", "pa$$2"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void reading_no_acl_stream_meta_is_not_authenticated_when_not_existing_credentials_are_passed()
        {
            Expect<NotAuthenticatedException>(() => ReadMeta("noacl-stream", "badlogin", "badpass"));
        }
    }

    [TestFixture, Category("LongRunning"), Category("Network")]
    public class write_stream_meta_security : security_test_fixture
    {
        [Test, Category("LongRunning"), Category("Network")]
        public void writing_meta_with_not_existing_credentials_is_not_authenticated()
        {
            Expect<NotAuthenticatedException>(() => WriteMeta("metawrite-stream", "badlogin", "badpass"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void writing_meta_to_stream_with_no_credentials_is_denied()
        {
            Expect<AccessDeniedException>(() => WriteMeta("metawrite-stream", null, null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void writing_meta_to_stream_with_not_authorized_user_credentials_is_denied()
        {
            Expect<AccessDeniedException>(() => WriteMeta("metawrite-stream", "user2", "pa$$2"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void writing_meta_to_stream_with_authorized_user_credentials_succeeds()
        {
            ExpectNoException(() => WriteMeta("metawrite-stream", "user1", "pa$$1"));
        }


        [Test, Category("LongRunning"), Category("Network")]
        public void writing_meta_to_no_acl_stream_succeeds_when_no_credentials_are_passed()
        {
            ExpectNoException(() => WriteMeta("noacl-stream", null, null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void writing_meta_to_no_acl_stream_succeeds_when_any_existing_user_credentials_are_passed()
        {
            ExpectNoException(() => WriteMeta("noacl-stream", "user1", "pa$$1"));
            ExpectNoException(() => WriteMeta("noacl-stream", "user2", "pa$$2"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void writing_meta_to_no_acl_stream_is_not_authenticated_when_not_existing_credentials_are_passed()
        {
            Expect<NotAuthenticatedException>(() => WriteMeta("noacl-stream", "badlogin", "badpass"));
        }
    }

    [TestFixture, Category("LongRunning"), Category("Network")]
    public class subscribe_to_stream_security : security_test_fixture
    {
        [Test, Category("LongRunning"), Category("Network")]
        public void subscribing_to_stream_with_not_existing_credentials_is_not_authenticated()
        {
            Expect<NotAuthenticatedException>(() => SubscribeToStream("read-stream", "badlogin", "badpass"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void subscribing_to_stream_with_no_credentials_is_denied()
        {
            Expect<AccessDeniedException>(() => SubscribeToStream("read-stream", null, null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void subscribing_to_stream_with_not_authorized_user_credentials_is_denied()
        {
            Expect<AccessDeniedException>(() => SubscribeToStream("read-stream", "user2", "pa$$2"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void reading_stream_with_authorized_user_credentials_succeeds()
        {
            ExpectNoException(() => SubscribeToStream("read-stream", "user1", "pa$$1"));
        }


        [Test, Category("LongRunning"), Category("Network")]
        public void subscribing_to_no_acl_stream_succeeds_when_no_credentials_are_passed()
        {
            ExpectNoException(() => SubscribeToStream("noacl-stream", null, null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void subscribing_to_no_acl_stream_succeeds_when_any_existing_user_credentials_are_passed()
        {
            ExpectNoException(() => SubscribeToStream("noacl-stream", "user1", "pa$$1"));
            ExpectNoException(() => SubscribeToStream("noacl-stream", "user2", "pa$$2"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void subscribing_to_no_acl_stream_is_not_authenticated_when_not_existing_credentials_are_passed()
        {
            Expect<NotAuthenticatedException>(() => SubscribeToStream("noacl-stream", "badlogin", "badpass"));
        }
    }

    [TestFixture, Category("LongRunning"), Category("Network")]
    public class subscribe_to_all_security : security_test_fixture
    {
        [Test, Category("LongRunning"), Category("Network")]
        public void subscribing_to_all_with_not_existing_credentials_is_not_authenticated()
        {
            Expect<NotAuthenticatedException>(() => SubscribeToAll("badlogin", "badpass"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void subscribing_to_all_with_no_credentials_is_denied()
        {
            Expect<AccessDeniedException>(() => SubscribeToAll(null, null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void subscribing_to_all_with_not_authorized_user_credentials_is_denied()
        {
            Expect<AccessDeniedException>(() => SubscribeToAll("user2", "pa$$2"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void subscribing_to_all_with_authorized_user_credentials_succeeds()
        {
            ExpectNoException(() => SubscribeToAll("user1", "pa$$1"));
        }
    }

    public abstract class security_test_fixture : SpecificationWithDirectoryPerTestFixture
    {
        private MiniNode _node;
        protected IEventStoreConnection Connection;

        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
            _node = new MiniNode(PathName);
            _node.Start();

            var userCreateEvent1 = new ManualResetEventSlim();
            _node.Node.MainQueue.Publish(new UserManagementMessage.Create(new CallbackEnvelope(m =>
            {
                Assert.IsTrue(m is UserManagementMessage.UpdateResult);
                var msg = (UserManagementMessage.UpdateResult) m;
                Assert.IsTrue(msg.Success);

                userCreateEvent1.Set();
            }), "user1", "Test User 1", new string[0], "pa$$1"));

            var userCreateEvent2 = new ManualResetEventSlim();
            _node.Node.MainQueue.Publish(new UserManagementMessage.Create(new CallbackEnvelope(m =>
            {
                Assert.IsTrue(m is UserManagementMessage.UpdateResult);
                var msg = (UserManagementMessage.UpdateResult)m;
                Assert.IsTrue(msg.Success);

                userCreateEvent2.Set();
            }), "user2", "Test User 2", new string[0], "pa$$2"));

            Assert.IsTrue(userCreateEvent1.Wait(5000), "User 1 creation failed");
            Assert.IsTrue(userCreateEvent2.Wait(5000), "User 2 creation failed");

            Connection = TestConnection.Create(_node.TcpEndPoint);
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
                                          StreamMetadata.Build().SetReadRole("user1"));
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

        protected void WriteMeta(string streamId, string login, string password)
        {
            Connection.SetStreamMetadata(streamId,
                                         ExpectedVersion.Any,
                                         Guid.NewGuid(),
                                         streamId == "metawrite-stream" ? StreamMetadata.Build().SetMetadataWriteRole("user1") : StreamMetadata.Build(),
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
            Assert.That(() =>
            {
                try
                {
                    action();
                }
                catch (Exception exc)
                {
                    throw;
                }
            },
            Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<T>());
        }

        protected void ExpectNoException(Action action)
        {
            Assert.That(() => action, Throws.Nothing);
        }

        private EventData[] CreateEvents()
        {
            return new[] { new EventData(Guid.NewGuid(), "some-type", false, new byte[] {1, 2, 3}, null) };
        }
    }
}
