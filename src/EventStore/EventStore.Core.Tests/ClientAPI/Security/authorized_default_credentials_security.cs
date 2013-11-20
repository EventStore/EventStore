﻿// Copyright (c) 2012, Event Store LLP
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
using EventStore.ClientAPI.SystemData;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Security
{
    [TestFixture, Category("LongRunning"), Category("Network")]
    public class authorized_default_credentials_security : AuthenticationTestBase
    {
        public authorized_default_credentials_security(): base(new UserCredentials("user1", "pa$$1"))
        {
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void all_operations_succeeds_when_passing_no_explicit_credentials()
        {
            ExpectNoException(() => ReadAllForward(null, null));
            ExpectNoException(() => ReadAllBackward(null, null));
            
            ExpectNoException(() => ReadEvent("read-stream", null, null));
            ExpectNoException(() => ReadStreamForward("read-stream", null, null));
            ExpectNoException(() => ReadStreamBackward("read-stream", null, null));
            
            ExpectNoException(() => WriteStream("write-stream", null, null));
            ExpectNoException(() =>
            {
                var trans = TransStart("write-stream", null, null);
                trans.Write();
                trans.Commit();
            });

            ExpectNoException(() => ReadMeta("metaread-stream", null, null));
            ExpectNoException(() => WriteMeta("metawrite-stream", null, null, "user1"));

            ExpectNoException(() => SubscribeToStream("read-stream", null, null));
            ExpectNoException(() => SubscribeToAll(null, null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void all_operations_are_not_authenticated_when_overriden_with_not_existing_credentials()
        {
            Expect<NotAuthenticatedException>(() => ReadAllForward("badlogin", "badpass"));
            Expect<NotAuthenticatedException>(() => ReadAllBackward("badlogin", "badpass"));

            Expect<NotAuthenticatedException>(() => ReadEvent("read-stream", "badlogin", "badpass"));
            Expect<NotAuthenticatedException>(() => ReadStreamForward("read-stream", "badlogin", "badpass"));
            Expect<NotAuthenticatedException>(() => ReadStreamBackward("read-stream", "badlogin", "badpass"));

            Expect<NotAuthenticatedException>(() => WriteStream("write-stream", "badlogin", "badpass"));
            Expect<NotAuthenticatedException>(() => TransStart("write-stream", "badlogin", "badpass"));
            {
                var transId = TransStart("write-stream", null, null).TransactionId;
                var trans = Connection.ContinueTransaction(transId, new UserCredentials("badlogin", "badpass"));
                ExpectNoException(() => trans.Write());
                Expect<NotAuthenticatedException>(() => trans.Commit());
            };

            Expect<NotAuthenticatedException>(() => ReadMeta("metaread-stream", "badlogin", "badpass"));
            Expect<NotAuthenticatedException>(() => WriteMeta("metawrite-stream", "badlogin", "badpass", "user1"));

            Expect<NotAuthenticatedException>(() => SubscribeToStream("read-stream", "badlogin", "badpass"));
            Expect<NotAuthenticatedException>(() => SubscribeToAll("badlogin", "badpass"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void all_operations_are_not_authorized_when_overriden_with_not_authorized_credentials()
        {
            Expect<AccessDeniedException>(() => ReadAllForward("user2", "pa$$2"));
            Expect<AccessDeniedException>(() => ReadAllBackward("user2", "pa$$2"));

            Expect<AccessDeniedException>(() => ReadEvent("read-stream", "user2", "pa$$2"));
            Expect<AccessDeniedException>(() => ReadStreamForward("read-stream", "user2", "pa$$2"));
            Expect<AccessDeniedException>(() => ReadStreamBackward("read-stream", "user2", "pa$$2"));

            Expect<AccessDeniedException>(() => WriteStream("write-stream", "user2", "pa$$2"));
            Expect<AccessDeniedException>(() => TransStart("write-stream", "user2", "pa$$2"));
            {
                var transId = TransStart("write-stream", null, null).TransactionId;
                var trans = Connection.ContinueTransaction(transId, new UserCredentials("user2", "pa$$2"));
                ExpectNoException(() => trans.Write());
                Expect<AccessDeniedException>(() => trans.Commit());
            };

            Expect<AccessDeniedException>(() => ReadMeta("metaread-stream", "user2", "pa$$2"));
            Expect<AccessDeniedException>(() => WriteMeta("metawrite-stream", "user2", "pa$$2", "user1"));

            Expect<AccessDeniedException>(() => SubscribeToStream("read-stream", "user2", "pa$$2"));
            Expect<AccessDeniedException>(() => SubscribeToAll("user2", "pa$$2"));
        }
    }
}