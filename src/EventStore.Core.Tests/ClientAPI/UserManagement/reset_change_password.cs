using System;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Http;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.UserManagement
{
    public class reset_password : TestWithUser
    {
        [Test]
        public void can_reset_password()
        {
            _manager.ResetPasswordAsync(_username, "foo", new UserCredentials("admin", "changeit")).Wait();
            var ex = Assert.Throws<AggregateException>(
                () => _manager.ChangePasswordAsync(_username, "password", "foobar", new UserCredentials(_username, "password")).Wait()
            );
            Assert.AreEqual(HttpStatusCode.Unauthorized, ((UserCommandFailedException)ex.InnerException).HttpStatusCode);
        }
    }

    public class change_password : TestWithUser
    {
        [Test]
        public void can_change_password()
        {
            _manager.ChangePasswordAsync(_username, "password", "fubar", new UserCredentials(_username, "password")).Wait();
            var ex = Assert.Throws<AggregateException>(
                () => _manager.ChangePasswordAsync(_username, "password", "foobar", new UserCredentials(_username, "password")).Wait()
            );
            Assert.AreEqual(HttpStatusCode.Unauthorized, ((UserCommandFailedException)ex.InnerException).HttpStatusCode);
        }
    }

}