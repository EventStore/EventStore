using System;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Common.Log;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.UserManagement;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.UserManagement
{
    public class get_current_user : SpecificationWithDirectoryPerTestFixture
    {
        private MiniNode _node;
        private UsersManager _manager;

        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
            _node = new MiniNode(PathName);
            _node.Start();
            _manager = new UsersManager(new NoopLogger(), _node.HttpEndPoint, TimeSpan.FromSeconds(5));
        }

        [TestFixtureTearDown]
        public override void TestFixtureTearDown()
        {
            _node.Shutdown();
            base.TestFixtureTearDown();
        }

        protected virtual IEventStoreConnection BuildConnection(MiniNode node)
        {
            return TestConnection.Create(node.TcpEndPoint);
        }


        [Test]
        public void returns_the_current_user()
        {
            var x = _manager.GetCurrentUserAsync(new UserCredentials("admin", "changeit")).Result;
            Assert.AreEqual("admin", x.LoginName);
            Assert.AreEqual("Event Store Administrator", x.FullName);
        }
    }
}