using System;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Common.Log;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Http;
using EventStore.ClientAPI.UserManagement;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.UserManagement
{
    [TestFixture]
    public class updating_a_user : SpecificationWithDirectoryPerTestFixture
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
        public void updating_a_user_with_null_username_throws()
        {
            Assert.Throws<ArgumentNullException>(() => _manager.UpdateUserAsync(null, "greg", new[] { "foo", "bar" }, new UserCredentials("admin", "changeit")));
        }

        [Test]
        public void updating_a_user_with_empty_username_throws()
        {
            Assert.Throws<ArgumentNullException>(() => _manager.UpdateUserAsync("", "greg", new[] { "foo", "bar" }, new UserCredentials("admin", "changeit")));
        }

        [Test]
        public void updating_a_user_with_null_name_throws()
        {
            Assert.Throws<ArgumentNullException>(() => _manager.UpdateUserAsync("greg", null, new[] { "foo", "bar" }, new UserCredentials("admin", "changeit")));
        }

        [Test]
        public void updating_a_user_with_empty_name_throws()
        {
            Assert.Throws<ArgumentNullException>(() => _manager.UpdateUserAsync("greg", "", new[] { "foo", "bar" }, new UserCredentials("admin", "changeit")));
        }

        [Test]
        public void updating_non_existing_user_throws()
        {
            var ex = Assert.Throws<AggregateException>(() => _manager.DeleteUserAsync(Guid.NewGuid().ToString(), new UserCredentials("admin", "changeit")).Wait());
            var realex = (UserCommandFailedException)ex.InnerException;
            Assert.AreEqual(HttpStatusCode.NotFound, realex.HttpStatusCode);
        }

        [Test]
        public void updating_a_user_with_parameters_can_be_read()
        {
            UserDetails d = null;
            _manager.CreateUserAsync("ouro", "ourofull", new[] {"foo", "bar"}, "password",
                new UserCredentials("admin", "changeit")).Wait();
            _manager.UpdateUserAsync("ouro", "something", new[] {"bar", "baz"}, new UserCredentials("admin", "changeit"))
                .Wait();
            Assert.DoesNotThrow(() =>
            {
                d = _manager.GetUserAsync("ouro", new UserCredentials("admin", "changeit")).Result;
            });
            Assert.AreEqual("ouro", d.LoginName);
            Assert.AreEqual("something", d.FullName);
            Assert.AreEqual("bar", d.Groups[0]);
            Assert.AreEqual("baz", d.Groups[1]);
        }
    }
}