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

namespace EventStore.Core.Tests.Http.Users
{
    namespace users
    {
        [TestFixture, Category("LongRunning")]
        class when_creating_a_user : HttpBehaviorSpecification
        {
            private HttpWebResponse _response;

            protected override void Given()
            {
            }

            protected override void When()
            {
                _response = MakeJsonPost(
                    "/users/", new {LoginName = "test1", FullName = "User Full Name", Password = "Pa55w0rd!"});
            }

            [Test]
            public void returns_created_status_code_and_location()
            {
                Assert.AreEqual(HttpStatusCode.Created, _response.StatusCode);
                Assert.AreEqual(MakeUrl("/users/test1"), _response.Headers[HttpResponseHeader.Location]);
            }
        }

        [TestFixture]
        class when_retrieving_a_user_details : HttpBehaviorSpecification
        {
            private JObject _response;

            protected override void Given()
            {
                MakeJsonPost("/users/", new {LoginName = "test1", FullName = "User Full Name", Password = "Pa55w0rd!"});
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
                AssertJson(
                    new
                        {
                            Success = true,
                            Error = "Success",
                            Data = new {LoginName = "test1", FullName = "User Full Name", Password___ = false}
                        }, _response);
            }
        }
    }
}
