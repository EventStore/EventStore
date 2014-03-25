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
            _node = new MiniNode(PathName, enableTrustedAuth: true);
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
                        }), SystemAccount.Principal, "adm", "Administrator User", new[] { SystemRoles.Admins}, "admpa$$"));

            Assert.IsTrue(userCreateEvent1.Wait(10000), "User 1 creation failed");
            Assert.IsTrue(userCreateEvent2.Wait(10000), "User 2 creation failed");
            Assert.IsTrue(adminCreateEvent2.Wait(10000), "Administrator User creation failed");

            Connection = TestConnection.Create(_node.TcpEndPoint, TcpType.Normal, _userCredentials);
            Connection.Connect();

            Connection.SetStreamMetadata("noacl-stream", ExpectedVersion.NoStream, StreamMetadata.Build());
            Connection.SetStreamMetadata("read-stream", ExpectedVersion.NoStream, StreamMetadata.Build().SetReadRole("user1"));
            Connection.SetStreamMetadata("write-stream", ExpectedVersion.NoStream, StreamMetadata.Build().SetWriteRole("user1"));
            Connection.SetStreamMetadata("metaread-stream", ExpectedVersion.NoStream, StreamMetadata.Build().SetMetadataReadRole("user1"));
            Connection.SetStreamMetadata("metawrite-stream", ExpectedVersion.NoStream, StreamMetadata.Build().SetMetadataWriteRole("user1"));

            Connection.SetStreamMetadata("$all", ExpectedVersion.Any, StreamMetadata.Build().SetReadRole("user1"), new UserCredentials("adm", "admpa$$"));

            Connection.SetStreamMetadata("$system-acl", ExpectedVersion.NoStream,
                                         StreamMetadata.Build()
                                                       .SetReadRole("user1")
                                                       .SetWriteRole("user1")
                                                       .SetMetadataReadRole("user1")
                                                       .SetMetadataWriteRole("user1"), new UserCredentials("adm", "admpa$$"));
            Connection.SetStreamMetadata("$system-adm", ExpectedVersion.NoStream,
                                         StreamMetadata.Build()
                                                       .SetReadRole(SystemRoles.Admins)
                                                       .SetWriteRole(SystemRoles.Admins)
                                                       .SetMetadataReadRole(SystemRoles.Admins)
                                                       .SetMetadataWriteRole(SystemRoles.Admins), new UserCredentials("adm", "admpa$$"));

            Connection.SetStreamMetadata("normal-all", ExpectedVersion.NoStream,
                                         StreamMetadata.Build()
                                                       .SetReadRole(SystemRoles.All)
                                                       .SetWriteRole(SystemRoles.All)
                                                       .SetMetadataReadRole(SystemRoles.All)
                                                       .SetMetadataWriteRole(SystemRoles.All));
            Connection.SetStreamMetadata("$system-all", ExpectedVersion.NoStream,
                                         StreamMetadata.Build()
                                                       .SetReadRole(SystemRoles.All)
                                                       .SetWriteRole(SystemRoles.All)
                                                       .SetMetadataReadRole(SystemRoles.All)
                                                       .SetMetadataWriteRole(SystemRoles.All), new UserCredentials("adm", "admpa$$"));
        }

        [TestFixtureTearDown]
        public override void TestFixtureTearDown()
        {
            _node.Shutdown();
            Connection.Close();
            base.TestFixtureTearDown();
        }

        protected void ReadEvent(string streamId, string login, string password)
        {
            Connection.ReadEvent(streamId, -1, false,
                                 login == null && password == null ? null : new UserCredentials(login, password));
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
            Connection.SetStreamMetadata(streamId, ExpectedVersion.Any,
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
            using (Connection.SubscribeToStream(streamId, false, (x, y) => { }, (x, y, z) => { },
                                                login == null && password == null ? null : new UserCredentials(login, password)))
            {
            }
        }

        protected void SubscribeToAll(string login, string password)
        {
            using (Connection.SubscribeToAll(false, (x, y) => { }, (x, y, z) => { },
                                             login == null && password == null ? null : new UserCredentials(login, password)))
            {
            }
        }

        protected string CreateStreamWithMeta(StreamMetadata metadata, string streamPrefix = null)
        {
            var stream = (streamPrefix ?? string.Empty) + TestContext.CurrentContext.Test.Name;
            Connection.SetStreamMetadata(stream, ExpectedVersion.NoStream,
                                         metadata, new UserCredentials("adm", "admpa$$"));
            return stream;
        }

        protected void DeleteStream(string streamId, string login, string password)
        {
            Connection.DeleteStream(streamId, ExpectedVersion.Any, true, 
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