using System;
using EventStore.Core.Bus;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Web.Playground
{
    public class TestController : CommunicationController
    {
        //private readonly IPublisher _networkSendQueue;

        public TestController(IPublisher publisher/*, IPublisher networkSendQueue*/)
            : base(publisher)
        {
            //_networkSendQueue = networkSendQueue;
        }

        protected override void SubscribeCore(IHttpService service)
        {
            Register(service, "/test1", Test1Handler);
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
    }
}
