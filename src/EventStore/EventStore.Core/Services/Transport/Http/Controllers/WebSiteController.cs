using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using EventStore.Core.Bus;
using EventStore.Projections.Core.Services;
using EventStore.Transport.Http;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services.Transport.Http.Controllers
{
    public class WebSiteController : CommunicationController
    {
        private readonly MiniWeb _miniWeb;

        public WebSiteController(IPublisher publisher)
            : base(publisher)
        {
            string fileSystemWebRoot = MiniWeb.GetWebRootFileSystemDirectory("EventStore.Web");
            _miniWeb = new MiniWeb("/web", Path.Combine(fileSystemWebRoot, @"web"));
        }

        protected override void SubscribeCore(IHttpService service, HttpMessagePipe pipe)
        {
            _miniWeb.RegisterControllerActions(service);
            RegisterRedirectAction(service, "", "/web/index.htm");
            RegisterRedirectAction(service, "/web/projections", "/web/list-projections.htm");
        }

        private static void RegisterRedirectAction(IHttpService service, string fromUrl, string toUrl)
        {
            service.RegisterControllerAction(
                new ControllerAction(
                    fromUrl, 
                    HttpMethod.Get, 
                    Codec.NoCodecs, 
                    new ICodec[] { Codec.ManualEncoding }, Codec.ManualEncoding),
                    (http, match) => http.Manager.Reply(
                        "Moved", 302, "Found", "text/plain",
                        new[]
                            {
                                new KeyValuePair<string, string>(
                                    "Location",   new Uri(match.BaseUri, toUrl).AbsoluteUri)
                            }, Console.WriteLine));
        }
    }
}
