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
using System.Text;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Http.Authentication;
using EventStore.Core.Services.Transport.Http.Messages;
using EventStore.Core.Tests.Bus.Helpers;
using EventStore.Transport.Http.EntityManagement;
using NUnit.Framework;
using System.Linq;

namespace EventStore.Core.Tests.Services.Transport.Http.Authentication
{
    namespace basic_http_authentication_provider
    {
        public class TestFixtureWithBasicHttpAuthenticationProvider
        {
            protected BasicHttpAuthenticationProvider _provider;
            private IODispatcher _ioDispatcher;
            protected InMemoryBus _bus;
            protected HttpEntity _entity;
            protected TestHandler<Message> _consumer;

            protected void SetUpProvider()
            {
                _bus = new InMemoryBus("test-bus");
                _consumer = new TestHandler<Message>();
                _bus.Subscribe(_consumer);
                _ioDispatcher = new IODispatcher(_bus, new PublishEnvelope(_bus));
                _provider = new BasicHttpAuthenticationProvider(_ioDispatcher, new StubPasswordHashAlgorithm());
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
                Assert.AreEqual(authenticatedHttpRequestMessages.Count, 0);
            }

        }
    }

    public class StubPasswordHashAlgorithm : PasswordHashAlgorithm
    {
        public override Tuple<string, string> Hash(string password)
        {
            return Tuple.Create(password, ReverseString(password));
        }

        public override bool Verify(string password, Tuple<string, string> hashed)
        {
            return password == hashed.Item1 && ReverseString(password) == hashed.Item2;
        }

        private static string ReverseString(string s)
        {
            return new String(s.Reverse().ToArray());
        }
    }
}

