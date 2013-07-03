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
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Security
{
    [TestFixture, Category("LongRunning"), Category("Network")]
    public class all_stream_with_no_acl_security : AuthenticationTestBase
    {
        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();

            Connection.SetStreamMetadata("$all", ExpectedVersion.Any, StreamMetadata.Build(), new UserCredentials("adm", "admpa$$"));

        }

        [Test, Category("LongRunning"), Category("Network")]
        public void write_to_all_is_never_allowed()
        {
            Expect<AccessDeniedException>(() => WriteStream("$all", null, null));
            Expect<AccessDeniedException>(() => WriteStream("$all", "user1", "pa$$1"));
            Expect<AccessDeniedException>(() => WriteStream("$all", "adm", "admpa$$"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void delete_of_all_is_never_allowed()
        {
            Expect<AccessDeniedException>(() => DeleteStream("$all", null, null));
            Expect<AccessDeniedException>(() => DeleteStream("$all", "user1", "pa$$1"));
            Expect<AccessDeniedException>(() => DeleteStream("$all", "adm", "admpa$$"));
        }


        [Test, Category("LongRunning"), Category("Network")]
        public void reading_and_subscribing_is_not_allowed_when_no_credentials_are_passed()
        {
            Expect<AccessDeniedException>(() => ReadEvent("$all", null, null));
            Expect<AccessDeniedException>(() => ReadStreamForward("$all", null, null));
            Expect<AccessDeniedException>(() => ReadStreamBackward("$all", null, null));
            Expect<AccessDeniedException>(() => ReadMeta("$all", null, null));
            Expect<AccessDeniedException>(() => SubscribeToStream("$all", null, null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void reading_and_subscribing_is_not_allowed_for_usual_user()
        {
            Expect<AccessDeniedException>(() => ReadEvent("$all", "user1", "pa$$1"));
            Expect<AccessDeniedException>(() => ReadStreamForward("$all", "user1", "pa$$1"));
            Expect<AccessDeniedException>(() => ReadStreamBackward("$all", "user1", "pa$$1"));
            Expect<AccessDeniedException>(() => ReadMeta("$all", "user1", "pa$$1"));
            Expect<AccessDeniedException>(() => SubscribeToStream("$all", "user1", "pa$$1"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void reading_and_subscribing_is_allowed_for_admin_user()
        {
            ExpectNoException(() => ReadEvent("$all", "adm", "admpa$$"));
            ExpectNoException(() => ReadStreamForward("$all", "adm", "admpa$$"));
            ExpectNoException(() => ReadStreamBackward("$all", "adm", "admpa$$"));
            ExpectNoException(() => ReadMeta("$all", "adm", "admpa$$"));
            ExpectNoException(() => SubscribeToStream("$all", "adm", "admpa$$"));
        }


        [Test, Category("LongRunning"), Category("Network")]
        public void meta_write_is_not_allowed_when_no_credentials_are_passed()
        {
            Expect<AccessDeniedException>(() => WriteMeta("$all", null, null, null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void meta_write_is_not_allowed_for_usual_user()
        {
            Expect<AccessDeniedException>(() => WriteMeta("$all", "user1", "pa$$1", null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void meta_write_is_allowed_for_admin_user()
        {
            ExpectNoException(() => WriteMeta("$all", "adm", "admpa$$", null));
        }
    }
}