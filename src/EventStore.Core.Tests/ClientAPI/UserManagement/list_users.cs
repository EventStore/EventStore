using EventStore.ClientAPI.SystemData;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.UserManagement
{
    public class list_users: TestWithNode
    {
        [Test]
        public void list_all_users_works()
        {
            _manager.CreateUserAsync("ouro", "ourofull", new[] { "foo", "bar" }, "ouro", new UserCredentials("admin", "changeit")).Wait();
            var x = _manager.ListAllAsync(new UserCredentials("admin", "changeit")).Result;
            Assert.AreEqual(2, x.Count);
            Assert.AreEqual("admin", x[0].LoginName);
            Assert.AreEqual("Event Store Administrator", x[0].FullName);
            Assert.AreEqual("ouro", x[1].LoginName);
            Assert.AreEqual("ourofull", x[1].FullName);

        } 
    }
}