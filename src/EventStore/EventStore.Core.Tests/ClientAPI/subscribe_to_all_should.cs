using System;
using System.Threading;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Services;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("LongRunning")]
    public class subscribe_to_all_should: SpecificationWithDirectory
    {
        private const int Timeout = 10000;
        
        private MiniNode _node;
        private IEventStoreConnection _conn;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            _node = new MiniNode(PathName, skipInitializeStandardUsersCheck: false);
            _node.Start();

            _conn = TestConnection.Create(_node.TcpEndPoint);
            _conn.Connect();
            _conn.SetStreamMetadata("$all", -1,
                                    StreamMetadata.Build().SetReadRole(SystemRoles.All),
                                    new UserCredentials(SystemUsers.Admin, SystemUsers.DefaultAdminPassword));
        }

        [TearDown]
        public override void TearDown()
        {
            _conn.Close();
            _node.Shutdown();
            base.TearDown();
        }

        [Test, Category("LongRunning")]
        public void allow_multiple_subscriptions()
        {
            const string stream = "subscribe_to_all_should_allow_multiple_subscriptions";
            using (var store = TestConnection.Create(_node.TcpEndPoint))
            {
                store.Connect();
                var appeared = new CountdownEvent(2);
                var dropped = new CountdownEvent(2);

                using (store.SubscribeToAll(false, (s, x) => appeared.Signal(), (s, r, e) => dropped.Signal()))
                using (store.SubscribeToAll(false, (s, x) => appeared.Signal(), (s, r, e) => dropped.Signal()))
                {
                    var create = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, TestEvent.NewTestEvent());
                    Assert.IsTrue(create.Wait(Timeout), "StreamCreateAsync timed out.");

                    Assert.IsTrue(appeared.Wait(Timeout), "Appeared countdown event timed out.");
                }
            }
        }

        [Test, Category("LongRunning")]
        public void catch_deleted_events_as_well()
        {
            const string stream = "subscribe_to_all_should_catch_created_and_deleted_events_as_well";
            using (var store = TestConnection.Create(_node.TcpEndPoint))
            {
                store.Connect();
                var appeared = new CountdownEvent(1);
                var dropped = new CountdownEvent(1);

                using (store.SubscribeToAll(false, (s, x) => appeared.Signal(), (s, r, e) => dropped.Signal()))
                {
                    var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream, hardDelete: true);
                    Assert.IsTrue(delete.Wait(Timeout), "DeleteStreamAsync timed out.");

                    Assert.IsTrue(appeared.Wait(Timeout), "Appeared countdown event didn't fire in time.");
                }
            }
        }
    }
}
