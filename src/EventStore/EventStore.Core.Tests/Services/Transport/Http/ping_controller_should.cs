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
using EventStore.Common.Utils;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.Services.Transport.Http.Codecs;
using EventStore.Transport.Http;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Transport.Http
{
    [MightyMooseIgnore]
    [TestFixture]
    public class ping_controller_should
    {
        private readonly IPEndPoint _serverEndPoint;
        private readonly PortableServer _portableServer;

        public ping_controller_should()
        {
            _serverEndPoint = new IPEndPoint(IPAddress.Loopback, 7778);
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
        public void respond_with_httpmessage_text_message()
        {
            var url = _serverEndPoint.ToHttpUrl("/ping?format=json");
            Func<HttpResponse, bool> verifier = response => Codec.Json.From<HttpMessage.TextMessage>(response.Body) != null;

            var result = _portableServer.StartServiceAndSendRequest(HttpBootstrap.RegisterPing, url, verifier);
            Assert.IsTrue(result.Item1, result.Item2);
        }

        [Test]
        public void return_response_in_json_if_requested_by_query_param_and_set_content_type_header()
        {
            var url = _serverEndPoint.ToHttpUrl("/ping?format=json");
            Func<HttpResponse, bool> verifier = response => string.Equals(response.Headers[HttpResponseHeader.ContentType],
                                                            ContentType.Json,
                                                            StringComparison.InvariantCultureIgnoreCase);

            var result = _portableServer.StartServiceAndSendRequest(HttpBootstrap.RegisterPing, url, verifier);
            Assert.IsTrue(result.Item1, result.Item2);
        }

        [Test]
        public void return_response_in_xml_if_requested_by_query_param_and_set_content_type_header()
        {
            var url = _serverEndPoint.ToHttpUrl("/ping?format=xml");
            Func<HttpResponse, bool> verifier = response => string.Equals(response.Headers[HttpResponseHeader.ContentType],
                                                            ContentType.Xml,
                                                            StringComparison.InvariantCultureIgnoreCase);

            var result = _portableServer.StartServiceAndSendRequest(HttpBootstrap.RegisterPing, url, verifier);
            Assert.IsTrue(result.Item1, result.Item2);
        }

        [Test]
        public void return_response_in_plaintext_if_requested_by_query_param_and_set_content_type_header()
        {
            var url = _serverEndPoint.ToHttpUrl("/ping?format=text");
            Func<HttpResponse, bool> verifier = response => string.Equals(response.Headers[HttpResponseHeader.ContentType],
                                                            ContentType.PlainText,
                                                            StringComparison.InvariantCultureIgnoreCase);

            var result = _portableServer.StartServiceAndSendRequest(HttpBootstrap.RegisterPing, url, verifier);
            Assert.IsTrue(result.Item1, result.Item2);
        }
    }
}
