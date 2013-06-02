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
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Services;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Security
{
    [TestFixture, Category("LongRunning"), Category("Network")]
    public class system_stream_security: AuthenticationTestBase
    {
        [Test, Category("LongRunning"), Category("Network")]
        public void operations_on_system_stream_with_no_acl_set_fail_for_non_admin()
        {
            Expect<AccessDeniedException>(() => ReadStreamForward("$system-no-acl", "user1", "pa$$1"));
            Expect<AccessDeniedException>(() => ReadStreamBackward("$system-no-acl", "user1", "pa$$1"));

            Expect<AccessDeniedException>(() => WriteStream("$system-no-acl", "user1", "pa$$1"));
            Expect<AccessDeniedException>(() => TransStart("$system-no-acl", "user1", "pa$$1"));
            {
                var transId = TransStart("$system-no-acl", "adm", "admpa$$").TransactionId;
                var trans = Connection.ContinueTransaction(transId, new UserCredentials("user1", "pa$$1"));
                ExpectNoException(() => trans.Write());
                Expect<AccessDeniedException>(() => trans.Commit());
            };

            Expect<AccessDeniedException>(() => ReadMeta("$system-no-acl", "user1", "pa$$1"));
            Expect<AccessDeniedException>(() => WriteMeta("$system-no-acl", "user1", "pa$$1", null));

            Expect<AccessDeniedException>(() => SubscribeToStream("$system-no-acl", "user1", "pa$$1"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void operations_on_system_stream_with_no_acl_set_succeed_for_admin()
        {
            ExpectNoException(() => ReadStreamForward("$system-no-acl", "adm", "admpa$$"));
            ExpectNoException(() => ReadStreamBackward("$system-no-acl", "adm", "admpa$$"));

            ExpectNoException(() => WriteStream("$system-no-acl", "adm", "admpa$$"));
            ExpectNoException(() => TransStart("$system-no-acl", "adm", "admpa$$"));
            {
                var transId = TransStart("$system-no-acl", "adm", "admpa$$").TransactionId;
                var trans = Connection.ContinueTransaction(transId, new UserCredentials("adm", "admpa$$"));
                ExpectNoException(() => trans.Write());
                ExpectNoException(() => trans.Commit());
            };

            ExpectNoException(() => ReadMeta("$system-no-acl", "adm", "admpa$$"));
            ExpectNoException(() => WriteMeta("$system-no-acl", "adm", "admpa$$", null));

            ExpectNoException(() => SubscribeToStream("$system-no-acl", "adm", "admpa$$"));
        }


        [Test, Category("LongRunning"), Category("Network")]
        public void operations_on_system_stream_with_acl_set_to_usual_user_fail_for_not_authorized_user()
        {
            Expect<AccessDeniedException>(() => ReadStreamForward("$system-acl", "user2", "pa$$2"));
            Expect<AccessDeniedException>(() => ReadStreamBackward("$system-acl", "user2", "pa$$2"));

            Expect<AccessDeniedException>(() => WriteStream("$system-acl", "user2", "pa$$2"));
            Expect<AccessDeniedException>(() => TransStart("$system-acl", "user2", "pa$$2"));
            {
                var transId = TransStart("$system-acl", "user1", "pa$$1").TransactionId;
                var trans = Connection.ContinueTransaction(transId, new UserCredentials("user2", "pa$$2"));
                ExpectNoException(() => trans.Write());
                Expect<AccessDeniedException>(() => trans.Commit());
            };

            Expect<AccessDeniedException>(() => ReadMeta("$system-acl", "user2", "pa$$2"));
            Expect<AccessDeniedException>(() => WriteMeta("$system-acl", "user2", "pa$$2", "user1"));

            Expect<AccessDeniedException>(() => SubscribeToStream("$system-acl", "user2", "pa$$2"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void operations_on_system_stream_with_acl_set_to_usual_user_succeed_for_that_user()
        {
            ExpectNoException(() => ReadStreamForward("$system-acl", "user1", "pa$$1"));
            ExpectNoException(() => ReadStreamBackward("$system-acl", "user1", "pa$$1"));

            ExpectNoException(() => WriteStream("$system-acl", "user1", "pa$$1"));
            ExpectNoException(() => TransStart("$system-acl", "user1", "pa$$1"));
            {
                var transId = TransStart("$system-acl", "adm", "admpa$$").TransactionId;
                var trans = Connection.ContinueTransaction(transId, new UserCredentials("user1", "pa$$1"));
                ExpectNoException(() => trans.Write());
                ExpectNoException(() => trans.Commit());
            };

            ExpectNoException(() => ReadMeta("$system-acl", "user1", "pa$$1"));
            ExpectNoException(() => WriteMeta("$system-acl", "user1", "pa$$1", "user1"));

            ExpectNoException(() => SubscribeToStream("$system-acl", "user1", "pa$$1"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void operations_on_system_stream_with_acl_set_to_usual_user_succeed_for_admin()
        {
            ExpectNoException(() => ReadStreamForward("$system-acl", "adm", "admpa$$"));
            ExpectNoException(() => ReadStreamBackward("$system-acl", "adm", "admpa$$"));

            ExpectNoException(() => WriteStream("$system-acl", "adm", "admpa$$"));
            ExpectNoException(() => TransStart("$system-acl", "adm", "admpa$$"));
            {
                var transId = TransStart("$system-acl", "user1", "pa$$1").TransactionId;
                var trans = Connection.ContinueTransaction(transId, new UserCredentials("adm", "admpa$$"));
                ExpectNoException(() => trans.Write());
                ExpectNoException(() => trans.Commit());
            };

            ExpectNoException(() => ReadMeta("$system-acl", "adm", "admpa$$"));
            ExpectNoException(() => WriteMeta("$system-acl", "adm", "admpa$$", "user1"));

            ExpectNoException(() => SubscribeToStream("$system-acl", "adm", "admpa$$"));
        }


        [Test, Category("LongRunning"), Category("Network")]
        public void operations_on_system_stream_with_acl_set_to_admins_fail_for_usual_user()
        {
            Expect<AccessDeniedException>(() => ReadStreamForward("$system-adm", "user1", "pa$$1"));
            Expect<AccessDeniedException>(() => ReadStreamBackward("$system-adm", "user1", "pa$$1"));

            Expect<AccessDeniedException>(() => WriteStream("$system-adm", "user1", "pa$$1"));
            Expect<AccessDeniedException>(() => TransStart("$system-adm", "user1", "pa$$1"));
            {
                var transId = TransStart("$system-adm", "adm", "admpa$$").TransactionId;
                var trans = Connection.ContinueTransaction(transId, new UserCredentials("user1", "pa$$1"));
                ExpectNoException(() => trans.Write());
                Expect<AccessDeniedException>(() => trans.Commit());
            };

            Expect<AccessDeniedException>(() => ReadMeta("$system-adm", "user1", "pa$$1"));
            Expect<AccessDeniedException>(() => WriteMeta("$system-adm", "user1", "pa$$1", SystemUserGroups.Admins));

            Expect<AccessDeniedException>(() => SubscribeToStream("$system-adm", "user1", "pa$$1"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void operations_on_system_stream_with_acl_set_to_admins_succeed_for_admin()
        {
            ExpectNoException(() => ReadStreamForward("$system-adm", "adm", "admpa$$"));
            ExpectNoException(() => ReadStreamBackward("$system-adm", "adm", "admpa$$"));

            ExpectNoException(() => WriteStream("$system-adm", "adm", "admpa$$"));
            ExpectNoException(() => TransStart("$system-adm", "adm", "admpa$$"));
            {
                var transId = TransStart("$system-adm", "adm", "admpa$$").TransactionId;
                var trans = Connection.ContinueTransaction(transId, new UserCredentials("adm", "admpa$$"));
                ExpectNoException(() => trans.Write());
                ExpectNoException(() => trans.Commit());
            };

            ExpectNoException(() => ReadMeta("$system-adm", "adm", "admpa$$"));
            ExpectNoException(() => WriteMeta("$system-adm", "adm", "admpa$$", SystemUserGroups.Admins));

            ExpectNoException(() => SubscribeToStream("$system-adm", "adm", "admpa$$"));
        }
    }
}