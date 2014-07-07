using System.Net;
using EventStore.Core.Services;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;
using Newtonsoft.Json.Linq;

namespace EventStore.Core.Tests.Http.Users
{
    namespace users
    {

        abstract class with_admin_user : HttpBehaviorSpecification
        {
            protected readonly ICredentials _admin = new NetworkCredential(
                SystemUsers.Admin, SystemUsers.DefaultAdminPassword);

            protected override bool GivenSkipInitializeStandardUsersCheck()
            {
                return false;
            }
        }

        [TestFixture, Category("LongRunning")]
        class when_creating_a_user : with_admin_user
        {
            private HttpWebResponse _response;

            protected override void Given()
            {
            }

            protected override void When()
            {
                _response = MakeJsonPost(
                    "/users/",
                    new
                        {
                            LoginName = "test1",
                            FullName = "User Full Name",
                            Groups = new[] {"admin", "other"},
                            Password = "Pa55w0rd!"
                        }, _admin);
            }

            [Test]
            public void returns_created_status_code_and_location()
            {
                Assert.AreEqual(HttpStatusCode.Created, _response.StatusCode);
                Assert.AreEqual(MakeUrl("/users/test1"), _response.Headers[HttpResponseHeader.Location]);
            }
        }

        [TestFixture, Category("LongRunning")]
        class when_retrieving_a_user_details : with_admin_user
        {
            private JObject _response;

            protected override void Given()
            {
                MakeJsonPost(
                    "/users/",
                    new
                        {
                            LoginName = "test1",
                            FullName = "User Full Name",
                            Groups = new[] {"admin", "other"},
                            Password = "Pa55w0rd!"
                        }, _admin);
            }

            protected override void When()
            {
                _response = GetJson<JObject>("/users/test1");
            }

            [Test]
            public void returns_ok_status_code()
            {
                Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
            }

            [Test]
            public void returns_valid_json_data()
            {
                HelperExtensions.AssertJson(
                    new
                        {
                            Success = true,
                            Error = "Success",
                            Data =
                        new
                            {
                                LoginName = "test1",
                                FullName = "User Full Name",
                                Groups = new[] {"admin", "other"},
                                Disabled = false,
                                Password___ = false
                            }
                        }, _response);
            }
        }

        [TestFixture, Category("LongRunning")]
        class when_creating_an_already_existing_user_account : with_admin_user
        {
            private HttpWebResponse _response;

            protected override void Given()
            {
                var response = MakeJsonPost(
                    "/users/", new {LoginName = "test1", FullName = "User Full Name", Password = "Pa55w0rd!"}, _admin);
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            }
       
            protected override void When()
            {
                _response = MakeJsonPost(
                    "/users/", new {LoginName = "test1", FullName = "User Full Name", Password = "Pa55w0rd!"}, _admin);
            }

            [Test]
            public void returns_created_status_code_and_location()
            {
                Assert.AreEqual(HttpStatusCode.Conflict, _response.StatusCode);
            }
        }

        [TestFixture, Category("LongRunning")]
        class when_disabling_an_enabled_user_account : with_admin_user
        {
            protected override void Given()
            {
                MakeJsonPost(
                    "/users/", new {LoginName = "test1", FullName = "User Full Name", Password = "Pa55w0rd!"}, _admin);
            }

            protected override void When()
            {
                MakePost("/users/test1/command/disable", _admin);
            }

            [Test]
            public void returns_ok_status_code()
            {
                Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
            }

            [Test]
            public void enables_it()
            {
                var jsonResponse = GetJson<JObject>("/users/test1");
                HelperExtensions.AssertJson(
                    new {Success = true, Error = "Success", Data = new {LoginName = "test1", Disabled = true}},
                    jsonResponse);
            }
        }

        [TestFixture, Category("LongRunning")]
        class when_enabling_a_disabled_user_account : with_admin_user
        {
            private HttpWebResponse _response;

            protected override void Given()
            {
                MakeJsonPost(
                    "/users/", new {LoginName = "test1", FullName = "User Full Name", Password = "Pa55w0rd!"}, _admin);
                MakePost("/users/test1/command/disable", _admin);
            }

            protected override void When()
            {
                _response = MakePost("/users/test1/command/enable", _admin);
            }

            [Test]
            public void returns_ok_status_code()
            {
                Assert.AreEqual(HttpStatusCode.OK, _response.StatusCode);
            }

            [Test]
            public void disables_it()
            {
                var jsonResponse = GetJson<JObject>("/users/test1");
                HelperExtensions.AssertJson(
                    new {Success = true, Error = "Success", Data = new {LoginName = "test1", Disabled = false}},
                    jsonResponse);
            }
        }

        [TestFixture, Category("LongRunning")]
        class when_updating_user_details : with_admin_user
        {
            private HttpWebResponse _response;

            protected override void Given()
            {
                MakeJsonPost(
                    "/users/", new {LoginName = "test1", FullName = "User Full Name", Password = "Pa55w0rd!"}, _admin);
            }

            protected override void When()
            {
                _response = MakeRawJsonPut("/users/test1", new {FullName = "Updated Full Name"}, _admin);
            }

            [Test]
            public void returns_ok_status_code()
            {
                Assert.AreEqual(HttpStatusCode.OK, _response.StatusCode);
            }

            [Test]
            public void updates_full_name()
            {
                var jsonResponse = GetJson<JObject>("/users/test1");
                HelperExtensions.AssertJson(
                    new { Success = true, Error = "Success", Data = new { FullName = "Updated Full Name" } }, jsonResponse);
            }
        }


        [TestFixture, Category("LongRunning")]
        class when_resetting_a_password : with_admin_user
        {
            private HttpWebResponse _response;

            protected override void Given()
            {
                MakeJsonPost(
                    "/users/", new {LoginName = "test1", FullName = "User Full Name", Password = "Pa55w0rd!"}, _admin);
            }

            protected override void When()
            {
                _response = MakeJsonPost(
                    "/users/test1/command/reset-password", new {NewPassword = "NewPassword!"}, _admin);
            }

            [Test]
            public void returns_ok_status_code()
            {
                Assert.AreEqual(HttpStatusCode.OK, _response.StatusCode);
            }

            [Test]
            public void can_change_password_using_the_new_password()
            {
                var response = MakeJsonPost(
                    "/users/test1/command/change-password",
                    new {CurrentPassword = "NewPassword!", NewPassword = "TheVeryNewPassword!"});
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            }
        }


        [TestFixture, Category("LongRunning")]
        class when_deleting_a_user_account : with_admin_user
        {
            private HttpWebResponse _response;

            protected override void Given()
            {
                MakeJsonPost(
                    "/users/", new {LoginName = "test1", FullName = "User Full Name", Password = "Pa55w0rd!"}, _admin);
            }

            protected override void When()
            {
                _response = MakeDelete("/users/test1", _admin);
            }

            [Test]
            public void returns_ok_status_code()
            {
                Assert.AreEqual(HttpStatusCode.OK, _response.StatusCode);
            }

            [Test]
            public void get_returns_not_found()
            {
                GetJson<JObject>("/users/test1");
                Assert.AreEqual(HttpStatusCode.NotFound, _lastResponse.StatusCode);
            }
        }
    }
}
