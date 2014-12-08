using System;
using EventStore.ClientAPI.Common.Log;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Http;
using EventStore.ClientAPI.UserManagement;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.UserManagement
{
    [TestFixture]
    public class deleting_a_user : SpecificationWithDirectoryPerTestFixture
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

        //[Test]
        //public void deleting_non_existing_user_throws()
        //{
        //    var ex = Assert.Throws<UserCommandFailedException>(() => _manager.DeleteUserAsync(Guid.NewGuid().ToString(), new UserCredentials("admin", "changeit")));
        //    Assert.AreEqual(HttpStatusCode.Forbidden, ex.HttpStatusCode);
        //}

        [Test]
        public void deleting_null_user_throws()
        {
            Assert.Throws<ArgumentNullException>(() => _manager.DeleteUserAsync(null, new UserCredentials("admin", "changeit")));
        }

        [Test]
        public void deleting_empty_user_throws()
        {
            Assert.Throws<ArgumentNullException>(() => _manager.DeleteUserAsync("", new UserCredentials("admin", "changeit")));
        }

        [Test]
        public void can_delete_a_user()
        {
            _manager.CreateUserAsync("ouro", "ouro", new[] { "foo", "bar" }, "ouro", new UserCredentials("admin", "changeit")).Wait();
            Assert.DoesNotThrow(() =>
            {
                var x =_manager.GetUserAsync("ouro", new UserCredentials("admin", "changeit")).Result;
            });
            _manager.DeleteUserAsync("ouro", new UserCredentials("admin", "changeit"));
            try
            {
                var x = _manager.GetUserAsync("ouro", new UserCredentials("admin", "changeit")).Result;
                Assert.Fail("should have thrown.");
            }
            catch (Exception ex)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, ((UserCommandFailedException) ex.InnerException).HttpStatusCode);
            }
        }
    }
}