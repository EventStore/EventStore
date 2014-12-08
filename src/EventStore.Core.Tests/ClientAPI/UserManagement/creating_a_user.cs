using System;
using System.Net;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Common.Log;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.UserManagement;
using EventStore.Core.Services;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.UserManagement
{
    public class creating_a_user : SpecificationWithDirectoryPerTestFixture
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
        public void creating_a_user_with_null_username_throws()
        {
            Assert.Throws<ArgumentNullException>(() => _manager.CreateUserAsync(null, "greg", new[] {"foo", "bar"}, "foofoofoo"));
        }

        [Test]
        public void creating_a_user_with_empty_username_throws()
        {
            Assert.Throws<ArgumentNullException>(() => _manager.CreateUserAsync("", "ouro", new[] { "foo", "bar" }, "foofoofoo"));
        }

        [Test]
        public void creating_a_user_with_null_name_throws()
        {
            Assert.Throws<ArgumentNullException>(() => _manager.CreateUserAsync("ouro", null, new[] { "foo", "bar" }, "foofoofoo"));
        }

        [Test]
        public void creating_a_user_with_empty_name_throws()
        {
            Assert.Throws<ArgumentNullException>(() => _manager.CreateUserAsync("ouro", "", new[] { "foo", "bar" }, "foofoofoo"));
        }


        [Test]
        public void creating_a_user_with_null_password_throws()
        {
            Assert.Throws<ArgumentNullException>(() => _manager.CreateUserAsync("ouro", "ouro", new[] { "foo", "bar" }, null));
        }

        [Test]
        public void creating_a_user_with_empty_password_throws()
        {
            Assert.Throws<ArgumentNullException>(() => _manager.CreateUserAsync("ouro", "ouro", new[] { "foo", "bar" }, ""));
        }

        [Test]
        public void creating_a_user_with_parameters_can_be_read()
        {
            _manager.CreateUserAsync("ouro", "ouro", new[] {"foo", "bar"}, "ouro", new UserCredentials("admin", "changeit")).Wait();
            Assert.DoesNotThrow(() => _manager.GetUserAsync("ouro", new UserCredentials("admin", "changeit")).Wait());
        }
    }
}