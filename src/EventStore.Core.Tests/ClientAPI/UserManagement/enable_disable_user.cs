using System;
using EventStore.ClientAPI.SystemData;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.UserManagement
{
    public class enable_disable_user : TestWithUser
    {
        [Test]
        public void can_enable_disable_user()
        {
            _manager.DisableAsync(_username, new UserCredentials("admin", "changeit")).Wait();
            Assert.Throws<AggregateException>(
                () => _manager.DisableAsync("foo", new UserCredentials(_username, "password")).Wait());
            _manager.EnableAsync(_username, new UserCredentials("admin", "changeit")).Wait();
            var c = _manager.GetCurrentUserAsync(new UserCredentials(_username, "password")).Result;
        }
    }
}