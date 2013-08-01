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
    public class stream_security_inheritance: AuthenticationTestBase
    {
        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();

            var settings = new SystemSettings(userStreamAcl: new StreamAcl(null, "user1", null, null, null),
                                              systemStreamAcl: new StreamAcl(null, "user1", null, null, null));
            Connection.AppendToStream(SystemStreams.SettingsStream,
                                      ExpectedVersion.Any,
                                      new UserCredentials("adm", "admpa$$"),
                                      new EventData(Guid.NewGuid(), "$settings", true, settings.ToJsonBytes(), null));

            Connection.SetStreamMetadata("user-no-acl", ExpectedVersion.NoStream,
                                         StreamMetadata.Build(), new UserCredentials("adm", "admpa$$"));
            Connection.SetStreamMetadata("user-w-diff", ExpectedVersion.NoStream,
                                         StreamMetadata.Build().SetWriteRole("user2"), new UserCredentials("adm", "admpa$$"));
            Connection.SetStreamMetadata("user-w-multiple", ExpectedVersion.NoStream,
                                         StreamMetadata.Build().SetWriteRoles(new[] {"user1", "user2"}), new UserCredentials("adm", "admpa$$"));
            Connection.SetStreamMetadata("user-w-restricted", ExpectedVersion.NoStream,
                                         StreamMetadata.Build().SetWriteRoles(new string[0]), new UserCredentials("adm", "admpa$$"));
            Connection.SetStreamMetadata("user-w-all", ExpectedVersion.NoStream,
                                         StreamMetadata.Build().SetWriteRole(SystemRoles.All), new UserCredentials("adm", "admpa$$"));

            Connection.SetStreamMetadata("user-r-restricted", ExpectedVersion.NoStream,
                                        StreamMetadata.Build().SetReadRole("user1"), new UserCredentials("adm", "admpa$$"));

            Connection.SetStreamMetadata("$sys-no-acl", ExpectedVersion.NoStream,
                                         StreamMetadata.Build(), new UserCredentials("adm", "admpa$$"));
            Connection.SetStreamMetadata("$sys-w-diff", ExpectedVersion.NoStream,
                                         StreamMetadata.Build().SetWriteRole("user2"), new UserCredentials("adm", "admpa$$"));
            Connection.SetStreamMetadata("$sys-w-multiple", ExpectedVersion.NoStream,
                                         StreamMetadata.Build().SetWriteRoles(new[] { "user1", "user2" }), new UserCredentials("adm", "admpa$$"));
            Connection.SetStreamMetadata("$sys-w-restricted", ExpectedVersion.NoStream,
                                         StreamMetadata.Build().SetWriteRoles(new string[0]), new UserCredentials("adm", "admpa$$"));
            Connection.SetStreamMetadata("$sys-w-all", ExpectedVersion.NoStream,
                                         StreamMetadata.Build().SetWriteRole(SystemRoles.All), new UserCredentials("adm", "admpa$$"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void acl_inheritance_is_working_properly_on_user_streams()
        {
            Expect<AccessDeniedException>(() => WriteStream("user-no-acl", null, null));
            ExpectNoException(() => WriteStream("user-no-acl", "user1", "pa$$1"));
            Expect<AccessDeniedException>(() => WriteStream("user-no-acl", "user2", "pa$$2"));
            ExpectNoException(() => WriteStream("user-no-acl", "adm", "admpa$$"));

            Expect<AccessDeniedException>(() => WriteStream("user-w-diff", null, null));
            Expect<AccessDeniedException>(() => WriteStream("user-w-diff", "user1", "pa$$1"));
            ExpectNoException(() => WriteStream("user-w-diff", "user2", "pa$$2"));
            ExpectNoException(() => WriteStream("user-w-diff", "adm", "admpa$$"));

            Expect<AccessDeniedException>(() => WriteStream("user-w-multiple", null, null));
            ExpectNoException(() => WriteStream("user-w-multiple", "user1", "pa$$1"));
            ExpectNoException(() => WriteStream("user-w-multiple", "user2", "pa$$2"));
            ExpectNoException(() => WriteStream("user-w-multiple", "adm", "admpa$$"));

            Expect<AccessDeniedException>(() => WriteStream("user-w-restricted", null, null));
            Expect<AccessDeniedException>(() => WriteStream("user-w-restricted", "user1", "pa$$1"));
            Expect<AccessDeniedException>(() => WriteStream("user-w-restricted", "user2", "pa$$2"));
            ExpectNoException(() => WriteStream("user-w-restricted", "adm", "admpa$$"));

            ExpectNoException(() => WriteStream("user-w-all", null, null));
            ExpectNoException(() => WriteStream("user-w-all", "user1", "pa$$1"));
            ExpectNoException(() => WriteStream("user-w-all", "user2", "pa$$2"));
            ExpectNoException(() => WriteStream("user-w-all", "adm", "admpa$$"));


            ExpectNoException(() => ReadEvent("user-no-acl", null, null));
            ExpectNoException(() => ReadEvent("user-no-acl", "user1", "pa$$1"));
            ExpectNoException(() => ReadEvent("user-no-acl", "user2", "pa$$2"));
            ExpectNoException(() => ReadEvent("user-no-acl", "adm", "admpa$$"));

            Expect<AccessDeniedException>(() => ReadEvent("user-r-restricted", null, null));
            ExpectNoException(() => ReadEvent("user-r-restricted", "user1", "pa$$1"));
            Expect<AccessDeniedException>(() => ReadEvent("user-r-restricted", "user2", "pa$$2"));
            ExpectNoException(() => ReadEvent("user-r-restricted", "adm", "admpa$$"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void acl_inheritance_is_working_properly_on_system_streams()
        {
            Expect<AccessDeniedException>(() => WriteStream("$sys-no-acl", null, null));
            ExpectNoException(() => WriteStream("$sys-no-acl", "user1", "pa$$1"));
            Expect<AccessDeniedException>(() => WriteStream("$sys-no-acl", "user2", "pa$$2"));
            ExpectNoException(() => WriteStream("$sys-no-acl", "adm", "admpa$$"));

            Expect<AccessDeniedException>(() => WriteStream("$sys-w-diff", null, null));
            Expect<AccessDeniedException>(() => WriteStream("$sys-w-diff", "user1", "pa$$1"));
            ExpectNoException(() => WriteStream("$sys-w-diff", "user2", "pa$$2"));
            ExpectNoException(() => WriteStream("$sys-w-diff", "adm", "admpa$$"));

            Expect<AccessDeniedException>(() => WriteStream("$sys-w-multiple", null, null));
            ExpectNoException(() => WriteStream("$sys-w-multiple", "user1", "pa$$1"));
            ExpectNoException(() => WriteStream("$sys-w-multiple", "user2", "pa$$2"));
            ExpectNoException(() => WriteStream("$sys-w-multiple", "adm", "admpa$$"));

            Expect<AccessDeniedException>(() => WriteStream("$sys-w-restricted", null, null));
            Expect<AccessDeniedException>(() => WriteStream("$sys-w-restricted", "user1", "pa$$1"));
            Expect<AccessDeniedException>(() => WriteStream("$sys-w-restricted", "user2", "pa$$2"));
            ExpectNoException(() => WriteStream("$sys-w-restricted", "adm", "admpa$$"));

            ExpectNoException(() => WriteStream("$sys-w-all", null, null));
            ExpectNoException(() => WriteStream("$sys-w-all", "user1", "pa$$1"));
            ExpectNoException(() => WriteStream("$sys-w-all", "user2", "pa$$2"));
            ExpectNoException(() => WriteStream("$sys-w-all", "adm", "admpa$$"));

            Expect<AccessDeniedException>(() => ReadEvent("$sys-no-acl", null, null));
            Expect<AccessDeniedException>(() => ReadEvent("$sys-no-acl", "user1", "pa$$1"));
            Expect<AccessDeniedException>(() => ReadEvent("$sys-no-acl", "user2", "pa$$2"));
            ExpectNoException(() => ReadEvent("$sys-no-acl", "adm", "admpa$$"));
        }
    }
}