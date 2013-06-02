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
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Security
{
    [TestFixture, Category("LongRunning"), Category("Network")]
    public class transactional_write_stream_security : AuthenticationTestBase
    {
        [Test, Category("LongRunning"), Category("Network")]
        public void starting_transaction_with_not_existing_credentials_is_not_authenticated()
        {
            Expect<NotAuthenticatedException>(() => TransStart("write-stream", "badlogin", "badpass"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void starting_transaction_to_stream_with_no_credentials_is_denied()
        {
            Expect<AccessDeniedException>(() => TransStart("write-stream", null, null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void starting_transaction_to_stream_with_not_authorized_user_credentials_is_denied()
        {
            Expect<AccessDeniedException>(() => TransStart("write-stream", "user2", "pa$$2"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void starting_transaction_to_stream_with_authorized_user_credentials_succeeds()
        {
            ExpectNoException(() => TransStart("write-stream", "user1", "pa$$1"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void starting_transaction_to_stream_with_admin_user_credentials_succeeds()
        {
            ExpectNoException(() => TransStart("write-stream", "adm", "admpa$$"));
        }


        [Test, Category("LongRunning"), Category("Network")]
        public void committing_transaction_with_not_existing_credentials_is_not_authenticated()
        {
            var transId = TransStart("write-stream", "user1", "pa$$1").TransactionId;
            var t2 = Connection.ContinueTransaction(transId, new UserCredentials("badlogin", "badpass"));
            t2.Write(CreateEvents());
            Expect<NotAuthenticatedException>(() => t2.Commit());
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void committing_transaction_to_stream_with_no_credentials_is_denied()
        {
            var transId = TransStart("write-stream", "user1", "pa$$1").TransactionId;
            var t2 = Connection.ContinueTransaction(transId);
            t2.Write();
            Expect<AccessDeniedException>(() => t2.Commit());
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void committing_transaction_to_stream_with_not_authorized_user_credentials_is_denied()
        {
            var transId = TransStart("write-stream", "user1", "pa$$1").TransactionId;
            var t2 = Connection.ContinueTransaction(transId, new UserCredentials("user2", "pa$$2"));
            t2.Write();
            Expect<AccessDeniedException>(() => t2.Commit());
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void committing_transaction_to_stream_with_authorized_user_credentials_succeeds()
        {
            var transId = TransStart("write-stream", "user1", "pa$$1").TransactionId;
            var t2 = Connection.ContinueTransaction(transId, new UserCredentials("user1", "pa$$1"));
            t2.Write();
            ExpectNoException(() => t2.Commit());
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void committing_transaction_to_stream_with_admin_user_credentials_succeeds()
        {
            var transId = TransStart("write-stream", "user1", "pa$$1").TransactionId;
            var t2 = Connection.ContinueTransaction(transId, new UserCredentials("adm", "admpa$$"));
            t2.Write();
            ExpectNoException(() => t2.Commit());
        }


        [Test, Category("LongRunning"), Category("Network")]
        public void transaction_to_no_acl_stream_succeeds_when_no_credentials_are_passed()
        {
            ExpectNoException(() =>
            {
                var t = TransStart("noacl-stream", null, null);
                t.Write(CreateEvents());
                t.Commit();
            });
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void transaction_to_no_acl_stream_is_not_authenticated_when_not_existing_credentials_are_passed()
        {
            Expect<NotAuthenticatedException>(() => TransStart("noacl-stream", "badlogin", "badpass"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void transaction_to_no_acl_stream_succeeds_when_any_existing_user_credentials_are_passed()
        {
            ExpectNoException(() =>
            {
                var t = TransStart("noacl-stream", "user1", "pa$$1");
                t.Write(CreateEvents());
                t.Commit();
            });
            ExpectNoException(() =>
            {
                var t = TransStart("noacl-stream", "user2", "pa$$2");
                t.Write(CreateEvents());
                t.Commit();
            });
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void transaction_to_no_acl_stream_succeeds_when_admin_user_credentials_are_passed()
        {
            ExpectNoException(() =>
            {
                var t = TransStart("noacl-stream", "adm", "admpa$$");
                t.Write(CreateEvents());
                t.Commit();
            });
        }
    }
}