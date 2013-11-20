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

using System;
using System.Net;
using EventStore.ClientAPI;
using EventStore.Core.Services;
using EventStore.Core.Tests.Http.Users;
using NUnit.Framework;
using Newtonsoft.Json.Linq;

namespace EventStore.Core.Tests.Http.StreamSecurity
{
    namespace stream_access
    {
        [TestFixture, Category("LongRunning")]
        class when_creating_a_secured_stream_by_posting_metadata: SpecificationWithUsers
        {
            private HttpWebResponse _response;

            protected override void When()
            {
                var metadata =
                    (StreamMetadata)
                    StreamMetadata.Build()
                                  .SetMetadataReadRole("admin")
                                  .SetMetadataWriteRole("admin")
                                  .SetReadRole("")
                                  .SetWriteRole("other");
                var jsonMetadata = metadata.AsJsonString();
                _response = MakeJsonPost(
                    TestMetadataStream,
                    new[]
                        {
                            new
                                {
                                    EventId = Guid.NewGuid(),
                                    EventType = SystemEventTypes.StreamMetadata,
                                    Data = new JRaw(jsonMetadata)
                                }
                        });
            }

            [Test]
            public void returns_ok_status_code()
            {
                Assert.AreEqual(HttpStatusCode.Created, _response.StatusCode);
            }

            [Test]
            public void refuses_to_post_event_as_anonymous()
            {
                var response = PostEvent(new {Some = "Data"});
                Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            }

            [Test]
            public void accepts_post_event_as_authorized_user()
            {
                var response = PostEvent(new {Some = "Data"}, GetCorrectCredentialsFor("user1"));
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            }

            [Test]
            public void accepts_post_event_as_authorized_user_by_trusted_auth()
            {
                var uri = MakeUrl (TestStream);
                var request2 = WebRequest.Create (uri);
                var httpWebRequest = (HttpWebRequest)request2;
                httpWebRequest.ConnectionGroupName = TestStream;
                httpWebRequest.Method = "POST";
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.UseDefaultCredentials = false;
                httpWebRequest.Headers.Add("ES-TrustedAuth", "root; admin, other");
                httpWebRequest.GetRequestStream()
                              .WriteJson(
                                  new[]
                                      {
                                          new
                                              {
                                                  EventId = Guid.NewGuid(),
                                                  EventType = "event-type",
                                                  Data = new {Some = "Data"}
                                              }
                                      });
                var request = httpWebRequest;
                var httpWebResponse = GetRequestResponse(request);
                var response = httpWebResponse;
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            }
        }
    }
}
