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
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Services;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Security
{
    [TestFixture, Category("LongRunning"), Category("Network")]
    public class multiple_role_security : AuthenticationTestBase
    {
        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
            
            var settings = new SystemSettings(
                new StreamAcl(new[]{"user1", "user2"}, new[]{"$admins", "user1"}, new[] {"user1", SystemRoles.All}, null, null),
                null);
            Connection.AppendToStream(SystemStreams.SettingsStream,
                                      ExpectedVersion.Any,
                                      new UserCredentials("adm", "admpa$$"),
                                      new EventData(Guid.NewGuid(), "$settings", true, settings.ToJsonBytes(), null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void multiple_roles_are_handled_correctly()
        {
            Expect<AccessDeniedException>(() => ReadEvent("usr-stream", null, null));
            ExpectNoException(() => ReadEvent("usr-stream", "user1", "pa$$1"));
            ExpectNoException(() => ReadEvent("usr-stream", "user2", "pa$$2"));
            ExpectNoException(() => ReadEvent("usr-stream", "adm", "admpa$$"));

            Expect<AccessDeniedException>(() => WriteStream("usr-stream", null, null));
            ExpectNoException(() => WriteStream("usr-stream", "user1", "pa$$1"));
            Expect<AccessDeniedException>(() => WriteStream("usr-stream", "user2", "pa$$2"));
            ExpectNoException(() => WriteStream("usr-stream", "adm", "admpa$$"));

            ExpectNoException(() => DeleteStream("usr-stream1", null, null));
            ExpectNoException(() => DeleteStream("usr-stream2", "user1", "pa$$1"));
            ExpectNoException(() => DeleteStream("usr-stream3", "user2", "pa$$2"));
            ExpectNoException(() => DeleteStream("usr-stream4", "adm", "admpa$$"));
        }
    }
}