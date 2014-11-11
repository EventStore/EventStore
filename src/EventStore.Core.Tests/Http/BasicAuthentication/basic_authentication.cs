using System;
using System.Net;
using EventStore.Core.Services;
using NUnit.Framework;
using Newtonsoft.Json.Linq;

namespace EventStore.Core.Tests.Http.BasicAuthentication
{
    namespace basic_authentication
    {
        public abstract class with_admin_user : HttpBehaviorSpecification
        {
            protected readonly ICredentials _admin = new NetworkCredential(
                SystemUsers.Admin, SystemUsers.DefaultAdminPassword);

            protected override bool GivenSkipInitializeStandardUsersCheck()
            {
                return false;
            }
        }

        [TestFixture, Category("LongRunning")]
        class when_requesting_an_unprotected_resource : with_admin_user
        {
            protected override void Given()
            {
            }

            protected override void When()
            {
                GetJson<JObject>("/test-anonymous");
            }

            [Test]
            public void returns_ok_status_code()
            {
                Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
            }

            [Test]
            public void does_not_return_www_authenticate_header()
            {
                Assert.Null(_lastResponse.Headers[HttpResponseHeader.WwwAuthenticate]);
            }
        }

        [TestFixture, Category("LongRunning")]
        class when_requesting_a_protected_resource : with_admin_user
        {
            protected override void Given()
            {
            }

            protected override void When()
            {
                GetJson<JObject>("/test1");
            }

            [Test]
            public void returns_unauthorized_status_code()
            {
                Assert.AreEqual(HttpStatusCode.Unauthorized, _lastResponse.StatusCode);
            }

            [Test]
            public void returns_www_authenticate_header()
            {
                Assert.NotNull(_lastResponse.Headers[HttpResponseHeader.WwwAuthenticate]);
            }
        }

        [TestFixture, Category("LongRunning")]
        class when_requesting_a_protected_resource_with_credentials_provided : with_admin_user
        {
            protected override void Given()
            {
                var response = MakeJsonPost(
                    "/users/", new {LoginName = "test1", FullName = "User Full Name", Password = "Pa55w0rd!"}, _admin);
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            }

            protected override void When()
            {
                GetJson<JObject>("/test1", credentials: new NetworkCredential("test1", "Pa55w0rd!"));
            }

            [Test]
            public void returns_ok_status_code()
            {
                Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
            }
        }

        [TestFixture, Category("LongRunning")]
        class when_requesting_a_protected_resource_with_invalid_credentials_provided : with_admin_user
        {
            protected override void Given()
            {
                var response = MakeJsonPost(
                    "/users/", new {LoginName = "test1", FullName = "User Full Name", Password = "Pa55w0rd!"}, _admin);
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            }

            protected override void When()
            {
                GetJson<JObject>("/test1", credentials: new NetworkCredential("test1", "InvalidPassword!"));
            }

            [Test]
            public void returns_unauthorized_status_code()
            {
                Assert.AreEqual(HttpStatusCode.Unauthorized, _lastResponse.StatusCode);
            }
        }

        [TestFixture, Category("LongRunning")]
        class when_requesting_a_protected_resource_with_credentials_of_disabled_user_account : with_admin_user
        {
            protected override void Given()
            {
                var response = MakeJsonPost(
                    "/users/", new {LoginName = "test1", FullName = "User Full Name", Password = "Pa55w0rd!"}, _admin);
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                response = MakePost("/users/test1/command/disable", _admin);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            }

            protected override void When()
            {
                GetJson<JObject>("/test1", credentials: new NetworkCredential("test1", "Pa55w0rd!"));
            }

            [Test]
            public void returns_unauthorized_status_code()
            {
                Assert.AreEqual(HttpStatusCode.Unauthorized, _lastResponse.StatusCode);
            }
        }

        [TestFixture, Category("LongRunning")]
        class when_requesting_a_protected_resource_with_credentials_of_deleted_user_account : with_admin_user
        {
            protected override void Given()
            {
                var response = MakeRawJsonPost(
                    "/users/", new {LoginName = "test1", FullName = "User Full Name", Password = "Pa55w0rd!"}, _admin);
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                Console.WriteLine("done with json post");
                response = MakeDelete("/users/test1", _admin);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            }

            protected override void When()
            {
                GetJson<JObject>("/test1", credentials: new NetworkCredential("test1", "Pa55w0rd!"));
            }

            [Test]
            public void returns_unauthorized_status_code()
            {
                Assert.AreEqual(HttpStatusCode.Unauthorized, _lastResponse.StatusCode);
            }
        }
    }
}
