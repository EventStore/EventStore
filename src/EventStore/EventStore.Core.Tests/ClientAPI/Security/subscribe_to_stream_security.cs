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
using EventStore.ClientAPI.Exceptions;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Security
{
    [TestFixture, Category("LongRunning"), Category("Network")]
    public class subscribe_to_stream_security : AuthenticationTestBase
    {
        [Test, Category("LongRunning"), Category("Network")]
        public void subscribing_to_stream_with_not_existing_credentials_is_not_authenticated()
        {
            Expect<NotAuthenticatedException>(() => SubscribeToStream("read-stream", "badlogin", "badpass"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void subscribing_to_stream_with_no_credentials_is_denied()
        {
            Expect<AccessDeniedException>(() => SubscribeToStream("read-stream", null, null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void subscribing_to_stream_with_not_authorized_user_credentials_is_denied()
        {
            Expect<AccessDeniedException>(() => SubscribeToStream("read-stream", "user2", "pa$$2"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void reading_stream_with_authorized_user_credentials_succeeds()
        {
            ExpectNoException(() => SubscribeToStream("read-stream", "user1", "pa$$1"));
        }


        [Test, Category("LongRunning"), Category("Network")]
        public void subscribing_to_no_acl_stream_succeeds_when_no_credentials_are_passed()
        {
            ExpectNoException(() => SubscribeToStream("noacl-stream", null, null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void subscribing_to_no_acl_stream_succeeds_when_any_existing_user_credentials_are_passed()
        {
            ExpectNoException(() => SubscribeToStream("noacl-stream", "user1", "pa$$1"));
            ExpectNoException(() => SubscribeToStream("noacl-stream", "user2", "pa$$2"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void subscribing_to_no_acl_stream_is_not_authenticated_when_not_existing_credentials_are_passed()
        {
            Expect<NotAuthenticatedException>(() => SubscribeToStream("noacl-stream", "badlogin", "badpass"));
        }
    }
}