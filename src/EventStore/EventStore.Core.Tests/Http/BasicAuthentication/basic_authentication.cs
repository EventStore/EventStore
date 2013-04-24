// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

using System.Net;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace EventStore.Core.Tests.Http.BasicAuthentication
{
    namespace basic_authentication
    {
        [TestFixture, Category("LongRunning")]
        class when_requesting_a_protected_resource : HttpBehaviorSpecification
        {
            protected override void Given()
            {
            }

            protected override void When()
            {
                var json = GetJson<JObject>("/test1");
            }

            [Test]
            public void returns_unauthorized_status_code()
            {
                Assert.AreEqual(HttpStatusCode.Unauthorized, _lastResponse.StatusCode);
            }

			[Test]
			public void returns_www_authenticate_header()
			{
				Assert.NotNull (_lastResponse.Headers [HttpResponseHeader.WwwAuthenticate]);
			}
		}

        [TestFixture, Category("LongRunning")]
        class when_requesting_a_protected_resource_with_credentials_provided: HttpBehaviorSpecification
        {
            private JObject _json;

            protected override void Given()
            {
                var response = MakeJsonPost(
                    "/users/", new {LoginName = "test1", FullName = "User Full Name", Password = "Pa55w0rd!"});
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            }

            protected override void When()
            {
                _json = GetJson<JObject>("/test1", credentials: new NetworkCredential("test1", "Pa55w0rd!"));
            }

            [Test]
            public void returns_ok_status_code()
            {
                Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
            }
        }

        [TestFixture, Category("LongRunning")]
        class when_requesting_a_protected_resource_with_invalid_credentials_provided: HttpBehaviorSpecification
        {
            private JObject _json;

            protected override void Given()
            {
                var response = MakeJsonPost(
                    "/users/", new {LoginName = "test1", FullName = "User Full Name", Password = "Pa55w0rd!"});
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            }

            protected override void When()
            {
                _json = GetJson<JObject>("/test1", credentials: new NetworkCredential("test1", "InvalidPassword!"));
            }

            [Test]
            public void returns_unauthorized_status_code()
            {
                Assert.AreEqual(HttpStatusCode.Unauthorized, _lastResponse.StatusCode);
            }
        }

        [TestFixture, Category("LongRunning")]
        class when_requesting_a_protected_resource_with_credentials_of_disabled_user_account : HttpBehaviorSpecification
        {
            private JObject _json;

            protected override void Given()
            {
                var response = MakeJsonPost(
                    "/users/", new {LoginName = "test1", FullName = "User Full Name", Password = "Pa55w0rd!"});
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                response = MakePost("/users/test1/command/disable");
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            }

            protected override void When()
            {
                _json = GetJson<JObject>("/test1", credentials: new NetworkCredential("test1", "Pa55w0rd!"));
            }

            [Test]
            public void returns_unauthorized_status_code()
            {
                Assert.AreEqual(HttpStatusCode.Unauthorized, _lastResponse.StatusCode);
            }
        }

        [TestFixture, Category("LongRunning")]
        class when_requesting_a_protected_resource_with_credentials_of_deleted_user_account : HttpBehaviorSpecification
        {
            private JObject _json;

            protected override void Given()
            {
                var response = MakeJsonPost(
                    "/users/", new {LoginName = "test1", FullName = "User Full Name", Password = "Pa55w0rd!"});
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                response = MakeDelete("/users/test1");
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            }

            protected override void When()
            {
                _json = GetJson<JObject>("/test1", credentials: new NetworkCredential("test1", "Pa55w0rd!"));
            }

            [Test]
            public void returns_unauthorized_status_code()
            {
                Assert.AreEqual(HttpStatusCode.Unauthorized, _lastResponse.StatusCode);
            }
        }

    }
}
