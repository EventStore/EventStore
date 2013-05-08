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
using System.Security.Principal;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Http.Authentication;
using EventStore.Core.Services.Transport.Http.Messages;
using EventStore.Core.Tests.Bus.Helpers;
using EventStore.Core.Tests.Helper;
using EventStore.Transport.Http.EntityManagement;
using NUnit.Framework;
using System.Linq;

namespace EventStore.Core.Tests.Services.Transport.Http.Authentication
{
    namespace basic_http_authentication_provider
    {
        public class TestFixtureWithBasicHttpAuthenticationProvider: TestFixtureWithExistingEvents
        {
            protected BasicHttpAuthenticationProvider _provider;
            protected IODispatcher _ioDispatcher;
            protected HttpEntity _entity;

            protected void SetUpProvider()
            {
                _ioDispatcher = new IODispatcher(_bus, new PublishEnvelope(_bus));
                _bus.Subscribe(_ioDispatcher.BackwardReader);
                _bus.Subscribe(_ioDispatcher.ForwardReader);
                _bus.Subscribe(_ioDispatcher.Writer);
                _bus.Subscribe(_ioDispatcher.StreamDeleter);

                PasswordHashAlgorithm passwordHashAlgorithm = new StubPasswordHashAlgorithm();
                var internalAuthenticationProvider = new InternalAuthenticationProvider(_ioDispatcher, passwordHashAlgorithm, 1000);
                _provider = new BasicHttpAuthenticationProvider(internalAuthenticationProvider);
            }
        }

        [TestFixture]
        public class when_handling_a_request_without_an_authorization_header : TestFixtureWithBasicHttpAuthenticationProvider
        {
            private bool _authenticateResult;

            [SetUp]
            public void SetUp()
            {
                SetUpProvider();
                _entity = HttpEntity.Test(null);
                _authenticateResult = _provider.Authenticate(new IncomingHttpRequestMessage(_entity, _bus));
            }

            [Test]
            public void returns_false()
            {
                Assert.IsFalse(_authenticateResult);
            }

            [Test]
            public void does_not_publish_authenticated_http_request_message()
            {
                var authenticatedHttpRequestMessages = _consumer.HandledMessages.OfType<AuthenticatedHttpRequestMessage>().ToList();
                Assert.AreEqual(0, authenticatedHttpRequestMessages.Count);
            }
        }

        [TestFixture]
        public class when_handling_a_request_with_correct_user_name_and_password : TestFixtureWithBasicHttpAuthenticationProvider
        {
            private bool _authenticateResult;

            protected override void Given()
            {
                base.Given();
                ExistingEvent("$user-user", "$user", null, "{LoginName:'user', Salt:'drowssap',Hash:'password'}");
            }

            [SetUp]
            public void SetUp()
            {
                SetUpProvider();
                _entity = HttpEntity.Test(new GenericPrincipal(new HttpListenerBasicIdentity("user", "password"), new string[0]));
                _authenticateResult = _provider.Authenticate(new IncomingHttpRequestMessage(_entity, _bus));
            }

            [Test]
            public void returns_true()
            {
                Assert.IsTrue(_authenticateResult);
            }

            [Test]
            public void publishes_authenticated_http_request_message_with_user()
            {
                var authenticatedHttpRequestMessages = _consumer.HandledMessages.OfType<AuthenticatedHttpRequestMessage>().ToList();
                Assert.AreEqual(1, authenticatedHttpRequestMessages.Count);
                var message = authenticatedHttpRequestMessages[0];
                Assert.AreEqual("user", message.Entity.User.Identity.Name);
                Assert.IsTrue(message.Entity.User.Identity.IsAuthenticated);
            }
        }

        [TestFixture]
        public class when_handling_multiple_requests_with_the_same_correct_user_name_and_password : TestFixtureWithBasicHttpAuthenticationProvider
        {
            private bool _authenticateResult;

            protected override void Given()
            {
                base.Given();
                ExistingEvent("$user-user", "$user", null, "{LoginName:'user', Salt:'drowssap',Hash:'password'}");
            }

            [SetUp]
            public void SetUp()
            {
                SetUpProvider();

                var entity = HttpEntity.Test(new GenericPrincipal(new HttpListenerBasicIdentity("user", "password"), new string[0]));
                _provider.Authenticate(new IncomingHttpRequestMessage(entity, _bus));

                _consumer.HandledMessages.Clear();

                _entity = HttpEntity.Test(new GenericPrincipal(new HttpListenerBasicIdentity("user", "password"), new string[0]));
                _authenticateResult = _provider.Authenticate(new IncomingHttpRequestMessage(_entity, _bus));
            }

            [Test]
            public void returns_true()
            {
                Assert.IsTrue(_authenticateResult);
            }

            [Test]
            public void publishes_authenticated_http_request_message_with_user()
            {
                var authenticatedHttpRequestMessages = _consumer.HandledMessages.OfType<AuthenticatedHttpRequestMessage>().ToList();
                Assert.AreEqual(1, authenticatedHttpRequestMessages.Count);
                var message = authenticatedHttpRequestMessages[0];
                Assert.AreEqual("user", message.Entity.User.Identity.Name);
                Assert.IsTrue(message.Entity.User.Identity.IsAuthenticated);
            }

            [Test]
            public void does_not_publish_any_read_requests()
            {
                Assert.AreEqual(0, _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsBackward>().Count());
                Assert.AreEqual(0, _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>().Count());
            }
        }

        [TestFixture]
        public class when_handling_a_request_with_incorrect_user_name_and_password : TestFixtureWithBasicHttpAuthenticationProvider
        {
            private bool _authenticateResult;

            protected override void Given()
            {
                base.Given();
                ExistingEvent("$user-user", "$user", null, "{LoginName:'user', Salt:'drowssap',Hash:'password'}");
            }

            [SetUp]
            public void SetUp()
            {
                SetUpProvider();
                _entity = HttpEntity.Test(new GenericPrincipal(new HttpListenerBasicIdentity("user", "password1"), new string[0]));
                _authenticateResult = _provider.Authenticate(new IncomingHttpRequestMessage(_entity, _bus));
            }

            [Test]
            public void returns_true()
            {
                Assert.IsTrue(_authenticateResult);
            }

            [Test]
            public void publishes_authenticated_http_request_message_with_user()
            {
                var authenticatedHttpRequestMessages = _consumer.HandledMessages.OfType<AuthenticatedHttpRequestMessage>().ToList();
                Assert.AreEqual(0, authenticatedHttpRequestMessages.Count);
            }
        }
    }

    public class StubPasswordHashAlgorithm : PasswordHashAlgorithm
    {
        public override void Hash(string password, out string hash, out string salt)
        {
            hash = password;
            salt = ReverseString(password);
        }

        public override bool Verify(string password, string hash, string salt)
        {
            return password == hash && ReverseString(password) == salt;
        }

        private static string ReverseString(string s)
        {
            return new String(s.Reverse().ToArray());
        }
    }
}

