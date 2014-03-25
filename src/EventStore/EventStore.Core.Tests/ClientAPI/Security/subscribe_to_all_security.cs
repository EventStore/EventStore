using EventStore.ClientAPI.Exceptions;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Security
{
    [TestFixture, Category("LongRunning"), Category("Network")]
    public class subscribe_to_all_security : AuthenticationTestBase
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

        [Test, Category("LongRunning"), Category("Network")]
        public void subscribing_to_all_with_admin_user_credentials_succeeds()
        {
            ExpectNoException(() => SubscribeToAll("adm", "admpa$$"));
        }
    }
}