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
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Http;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Client;

namespace EventStore.Core.Tests.Infrastructure.Services.Transport.Http
{
    public class PortableServer
    {
        public bool IsListening
        {
            get
            {
                return _service.IsListening;
            }
        }

        public IHttpClient BuiltInClient
        {
            get
            {
                return _client;
            }
        }

        private InMemoryBus _bus;
        private HttpService _service;
        private HttpAsyncClient _client;

        private readonly IPEndPoint _serverEndPoint;

        public PortableServer(IPEndPoint serverEndPoint)
        {
            Ensure.NotNull(serverEndPoint, "serverEndPoint");
            _serverEndPoint = serverEndPoint;
        }

        public void SetUp()
        {
            _bus = new InMemoryBus(string.Format("bus_{0}", _serverEndPoint.Port));

            _service = new HttpService(_bus, new[]{_serverEndPoint.ToHttpUrl()});
            _client = new HttpAsyncClient();

            HttpBootstrap.Subscribe(_bus, _service);
        }

        public void TearDown()
        {
            HttpBootstrap.Unsubscribe(_bus, _service);

            _service.Shutdown();
        }

        public void Publish(Message message)
        {
            _bus.Publish(message);
        }

        public Tuple<bool, string> StartServiceAndSendRequest(Action<HttpService> bootstrap,
                                                       string requestUrl,
                                                       Func<HttpResponse, bool> verifyResponse)
        {
            _bus.Publish(new SystemMessage.SystemInit());

            bootstrap(_service);

            var signal = new AutoResetEvent(false);
            var success = false;
            var error = string.Empty;

            _client.Get(requestUrl,
                        response =>
                            {
                                success = verifyResponse(response);
                                signal.Set();
                            },
                        exception =>
                            {
                                success = false;
                                error = exception.Message;
                                signal.Set();
                            });

            signal.WaitOne();
            return new Tuple<bool, string>(success, error);
        }
    }
}
