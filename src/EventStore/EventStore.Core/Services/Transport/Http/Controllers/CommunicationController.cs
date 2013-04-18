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
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Client;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services.Transport.Http.Controllers
{
    public abstract class CommunicationController : IController
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<CommunicationController>();

        private readonly IPublisher _publisher;
        protected readonly HttpAsyncClient Client;
        private readonly ICodec[] DefaultCodecs = new ICodec[] {Codec.Json, Codec.Xml};

        protected CommunicationController(IPublisher publisher)
        {
            Ensure.NotNull(publisher, "publisher");

            _publisher = publisher;
            Client = new HttpAsyncClient();
        }

        public void Publish(Message message)
        {
            Ensure.NotNull(message, "message");
            _publisher.Publish(message);
        }

        public void Subscribe(IHttpService service, HttpMessagePipe pipe)
        {
            Ensure.NotNull(service, "service");
            Ensure.NotNull(pipe, "pipe");

            SubscribeCore(service, pipe);
        }

        protected abstract void SubscribeCore(IHttpService service, HttpMessagePipe pipe);

        protected void SendBadRequest(HttpEntityManager httpEntityManager, string reason)
        {
            httpEntityManager.ReplyStatus(HttpStatusCode.BadRequest,
                                       reason,
                                       e => Log.ErrorException(e, "Error while closing http connection (bad request)"));
        }

        protected void SendOk(HttpEntityManager httpEntityManager)
        {
            httpEntityManager.ReplyStatus(HttpStatusCode.OK,
                                       "OK",
                                       e => Log.ErrorException(e, "Error while closing http connection (ok)"));
        }

        protected void Register(IHttpService service, string uriTemplate, string httpMethod, 
                                Action<HttpEntityManager, UriTemplateMatch> handler, ICodec[] requestCodecs, ICodec[] responseCodecs)
        {
            service.RegisterControllerAction(new ControllerAction(uriTemplate, httpMethod, requestCodecs, responseCodecs), handler);
        }

        protected void LogError(Exception obj)
        {
            throw new NotImplementedException();
        }

        protected void RegisterTextBody(
            IHttpService service, string uriTemplate, string httpMethod, Action<HttpEntityManager, string> action)
        {
            Register(
                service, uriTemplate, httpMethod, (http, match) => http.ReadTextRequestAsync(action, LogError),
                DefaultCodecs, DefaultCodecs);
        }

        protected void RegisterTextBody(
            IHttpService service, string uriTemplate, string httpMethod,
            Action<HttpEntityManager, UriTemplateMatch, string> action)
        {
            Register(
                service, uriTemplate, httpMethod,
                (http, match) => http.ReadTextRequestAsync((manager, s) => action(manager, match, s), LogError),
                DefaultCodecs, DefaultCodecs);
        }

        protected void RegisterUrlBased(
            IHttpService service, string uriTemplate, string httpMethod, Action<HttpEntityManager, UriTemplateMatch> action)
        {
            Register(service, uriTemplate, httpMethod, action, Codec.NoCodecs, DefaultCodecs);
        }
    }
}