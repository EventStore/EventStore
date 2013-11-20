﻿using System;
using System.Text;
using EventStore.Core.Bus;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;
using EventStore.Common.Utils;

namespace EventStore.Core.Tests.Http
{
    public class TestController : CommunicationController
    {
        private readonly IPublisher _networkSendQueue;

        public TestController(IPublisher publisher, IPublisher networkSendQueue)
            : base(publisher)
        {
            _networkSendQueue = networkSendQueue;
        }

        protected override void SubscribeCore(IHttpService service)
        {
            Register(service, "/test1", Test1Handler);
            Register(service, "/test-anonymous", TestAnonymousHandler);
            Register(service, "/test-encoding/{a}?b={b}", TestEncodingHandler);
            Register(service, "/test-encoding-reserved-%20?b={b}", (manager, match) => TestEncodingHandler(manager, match, "%20"));
            Register(service, "/test-encoding-reserved-%24?b={b}", (manager, match) => TestEncodingHandler(manager, match, "%24"));
            Register(service, "/test-encoding-reserved-%25?b={b}", (manager, match) => TestEncodingHandler(manager, match, "%25"));
            Register(service, "/test-encoding-reserved- ?b={b}", (manager, match) => TestEncodingHandler(manager, match, " "));
            Register(service, "/test-encoding-reserved-$?b={b}", (manager, match) => TestEncodingHandler(manager, match, "$"));
            Register(service, "/test-encoding-reserved-%?b={b}", (manager, match) => TestEncodingHandler(manager, match, "%"));
        }

        private void Register(
            IHttpService service, string uriTemplate, Action<HttpEntityManager, UriTemplateMatch> handler,
            string httpMethod = HttpMethod.Get)
        {
            Register(service, uriTemplate, httpMethod, handler, Codec.NoCodecs, new ICodec[] {Codec.ManualEncoding});
        }

        private void Test1Handler(HttpEntityManager http, UriTemplateMatch match)
        {
            if (http.User != null) 
                http.Reply("OK", 200, "OK", "text/plain");
            else 
                http.Reply("Please authenticate yourself", 401, "Unauthorized", "text/plain");
        }

        private void TestAnonymousHandler(HttpEntityManager http, UriTemplateMatch match)
        {
            if (http.User != null)
                http.Reply("ERROR", 500, "ERROR", "text/plain");
            else 
                http.Reply("OK", 200, "OK", "text/plain");
        }

        private void TestEncodingHandler(HttpEntityManager http, UriTemplateMatch match)
        {
            var a = match.BoundVariables["a"];
            var b = match.BoundVariables["b"];

            http.Reply(new { a = a, b = b, rawSegment = http.RequestedUrl.Segments[2] }.ToJson(), 200, "OK", "application/json");
        }

        private void TestEncodingHandler(HttpEntityManager http, UriTemplateMatch match, string a)
        {
            var b = match.BoundVariables["b"];

            http.Reply(
                new
                    {
                        a = a,
                        b = b,
                        rawSegment = http.RequestedUrl.Segments[1],
                        requestUri = match.RequestUri,
                        rawUrl = http.HttpEntity.Request.RawUrl
                    }.ToJson(), 200, "OK", "application/json");
        }
    }
}