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
using System.Linq;
using System.Net;
using EventStore.ClientAPI;
using EventStore.Core.Services;
using NUnit.Framework;
using Newtonsoft.Json.Linq;

namespace EventStore.Core.Tests.Http.StreamSecurity
{
    abstract class SpecificationWithUsers : HttpBehaviorSpecification
    {
        protected override void Given()
        {
            PostUser("user1", "User 1", "user1!", "other");
            PostUser("user2", "User 2", "user2!", "other");
            PostUser("guest", "Guest", "guest!");
        }

        protected readonly ICredentials _admin = new NetworkCredential(
            SystemUsers.Admin, SystemUsers.DefaultAdminPassword);

        protected override bool GivenSkipInitializeStandardUsersCheck()
        {
            return false;
        }

        protected void PostUser(string login, string userFullName, string password, params string[] groups)
        {
            var response = MakeJsonPost(
                "/users/", new {LoginName = login + Tag, FullName = userFullName, Groups = groups, Password = password},
                _admin);
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        }

        protected string PostMetadata(StreamMetadata metadata)
        {
            var response = MakeJsonPost(
                TestMetadataStream, new[] {new {EventId = Guid.NewGuid(), EventType = "event-type", Data = metadata}});
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            return response.Headers[HttpResponseHeader.Location];
        }

        protected string PostEvent(int i)
        {
            var response = MakeJsonPost(
                TestStream, new[] {new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {Number = i}}});
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            return response.Headers[HttpResponseHeader.Location];
        }

        protected HttpWebResponse PostEvent<T>(T data, ICredentials credentials = null)
        {
            return MakeJsonPost(
                TestStream, new[] {new {EventId = Guid.NewGuid(), EventType = "event-type", Data = data}}, credentials);
        }

        protected string GetLink(JObject feed, string relation)
        {
            var rel = (from JObject link in feed["links"]
                       from JProperty attr in link
                       where attr.Name == "relation" && (string) attr.Value == relation
                       select link).SingleOrDefault();
            return (rel == null) ? null : (string) rel["uri"];
        }

        protected NetworkCredential GetCorrectCredentialsFor(string user)
        {
            return new NetworkCredential(user+ Tag, user + "!");
        }
    }
}
