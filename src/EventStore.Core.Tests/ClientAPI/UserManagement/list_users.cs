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
    public class list_users: TestWithNode
    {
        [Test]
        public void list_all_users_works()
        {
            //TODO GFY THIS 404s on node without projections running now.
            //var x = _manager.ListAllAsync(new UserCredentials("admin", "changeit")).Result;
            //Assert.AreEqual(1, x.Count);
        } 
    }
}