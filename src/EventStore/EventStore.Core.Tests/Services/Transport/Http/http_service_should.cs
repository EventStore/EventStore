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
using System.Threading;
using EventStore.Core.Messages;
using EventStore.Transport.Http;
using NUnit.Framework;
using EventStore.Common.Utils;
using System.Linq;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace EventStore.Core.Tests.Services.Transport.Http
{
    [TestFixture]
    public class http_service_should
    {
        private readonly IPEndPoint _serverEndPoint;
        private readonly PortableServer _portableServer;

        public http_service_should()
        {
            _serverEndPoint = new IPEndPoint(IPAddress.Loopback, 7777);
            _portableServer = new PortableServer(_serverEndPoint);
        }

        [SetUp]
        public void SetUp()
        {
            _portableServer.SetUp();
        }

        [TearDown]
        public void TearDown()
        {
            _portableServer.TearDown();
        }

        [Test]
        [Category("Network")]
        public void start_after_system_message_system_init_published()
        {
            Assert.IsFalse(_portableServer.IsListening);
            _portableServer.Publish(new SystemMessage.SystemInit());
            Assert.IsTrue(_portableServer.IsListening);
        }

        [Test]
        [Category("Network")]
        public void ignore_any_shutdown_messages()
        {
            _portableServer.Publish(new SystemMessage.SystemInit());
            Assert.IsTrue(_portableServer.IsListening);

            _portableServer.Publish(new SystemMessage.BecomeShutdown());
            _portableServer.Publish(new SystemMessage.BecomeShuttingDown());
            Assert.IsTrue(_portableServer.IsListening);
        }

        [Test]
        [Category("Network")]
        public void reply_with_404_to_every_request_when_there_are_no_registered_controllers()
        {
            var requests = new[] {"/ping", "/streams", "/gossip", "/stuff", "/notfound", "/magic/url.exe"};
            var successes = new bool[requests.Length];
            var errors = new string[requests.Length];
            var signals = new AutoResetEvent[requests.Length];
            for (var i = 0; i < signals.Length; i++)
                signals[i] = new AutoResetEvent(false);

            _portableServer.Publish(new SystemMessage.SystemInit());

            for (var i = 0; i < requests.Length; i++)
            {
                var i1 = i;
                _portableServer.BuiltInClient.Get(_serverEndPoint.ToHttpUrl(requests[i]),
                            response =>
                                {
                                    successes[i1] = response.HttpStatusCode == (int) HttpStatusCode.NotFound;
                                    signals[i1].Set();
                                },
                            exception =>
                                {
                                    successes[i1] = false;
                                    errors[i1] = exception.Message;
                                    signals[i1].Set();
                                });
            }

            foreach (var signal in signals)
                signal.WaitOne();

            Assert.IsTrue(successes.All(x => x), string.Join(";", errors.Where(e => !string.IsNullOrEmpty(e))));
        }

        [Test]
        [Category("Network")]
        public void handle_invalid_characters_in_url()
        {
            var url = _serverEndPoint.ToHttpUrl("/ping^\"");
            Func<HttpResponse, bool> verifier = response => string.IsNullOrEmpty(response.Body) &&
                                                            response.HttpStatusCode == (int) HttpStatusCode.NotFound;

            var result = _portableServer.StartServiceAndSendRequest(HttpBootstrap.RegisterPing, url, verifier);
            Assert.IsTrue(result.Item1, result.Item2);
        }
    }
}
