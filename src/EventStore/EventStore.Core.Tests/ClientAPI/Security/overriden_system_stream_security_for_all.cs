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
    public class overriden_system_stream_security_for_all : AuthenticationTestBase
    {
        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
            
            var settings = new SystemSettings(
                userStreamAcl: null,
                systemStreamAcl: new StreamAcl(SystemRoles.All, SystemRoles.All, SystemRoles.All, SystemRoles.All, SystemRoles.All));
            Connection.SetSystemSettings(settings, new UserCredentials("adm", "admpa$$"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void operations_on_system_stream_succeeds_for_user()
        {
            const string stream = "$sys-authorized-user";
            ExpectNoException(() => ReadEvent(stream, "user1", "pa$$1"));
            ExpectNoException(() => ReadStreamForward(stream, "user1", "pa$$1"));
            ExpectNoException(() => ReadStreamBackward(stream, "user1", "pa$$1"));

            ExpectNoException(() => WriteStream(stream, "user1", "pa$$1"));
            ExpectNoException(() => TransStart(stream, "user1", "pa$$1"));
            {
                var transId = TransStart(stream, "adm", "admpa$$").TransactionId;
                var trans = Connection.ContinueTransaction(transId, new UserCredentials("user1", "pa$$1"));
                ExpectNoException(() => trans.Write());
                ExpectNoException(() => trans.Commit());
            };

            ExpectNoException(() => ReadMeta(stream, "user1", "pa$$1"));
            ExpectNoException(() => WriteMeta(stream, "user1", "pa$$1", null));

            ExpectNoException(() => SubscribeToStream(stream, "user1", "pa$$1"));

            ExpectNoException(() => DeleteStream(stream, "user1", "pa$$1"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void operations_on_system_stream_fail_for_anonymous_user()
        {
            const string stream = "$sys-anonymous-user";
            ExpectNoException(() => ReadEvent(stream, null, null));
            ExpectNoException(() => ReadStreamForward(stream, null, null));
            ExpectNoException(() => ReadStreamBackward(stream, null, null));

            ExpectNoException(() => WriteStream(stream, null, null));
            ExpectNoException(() => TransStart(stream, null, null));
            {
                var transId = TransStart(stream, "adm", "admpa$$").TransactionId;
                var trans = Connection.ContinueTransaction(transId, null);
                ExpectNoException(() => trans.Write());
                ExpectNoException(() => trans.Commit());
            };

            ExpectNoException(() => ReadMeta(stream, null, null));
            ExpectNoException(() => WriteMeta(stream, null, null, null));

            ExpectNoException(() => SubscribeToStream(stream, null, null));

            ExpectNoException(() => DeleteStream(stream, null, null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void operations_on_system_stream_succeed_for_admin()
        {
            const string stream = "$sys-admin";
            ExpectNoException(() => ReadEvent(stream, "adm", "admpa$$"));
            ExpectNoException(() => ReadStreamForward(stream, "adm", "admpa$$"));
            ExpectNoException(() => ReadStreamBackward(stream, "adm", "admpa$$"));

            ExpectNoException(() => WriteStream(stream, "adm", "admpa$$"));
            ExpectNoException(() => TransStart(stream, "adm", "admpa$$"));
            {
                var transId = TransStart(stream, "adm", "admpa$$").TransactionId;
                var trans = Connection.ContinueTransaction(transId, new UserCredentials("adm", "admpa$$"));
                ExpectNoException(() => trans.Write());
                ExpectNoException(() => trans.Commit());
            };

            ExpectNoException(() => ReadMeta(stream, "adm", "admpa$$"));
            ExpectNoException(() => WriteMeta(stream, "adm", "admpa$$", null));

            ExpectNoException(() => SubscribeToStream(stream, "adm", "admpa$$"));

            ExpectNoException(() => DeleteStream(stream, "adm", "admpa$$"));
        }
    }
}