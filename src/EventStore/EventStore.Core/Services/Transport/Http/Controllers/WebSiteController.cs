using System;
using System.IO;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Util;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services.Transport.Http.Controllers
{
    public class WebSiteController : CommunicationController
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<WebSiteController>();

        private readonly NodeSubsystems[] _enabledNodeSubsystems;

        private readonly MiniWeb _commonWeb;
        private readonly MiniWeb _singleNodeWeb;

        public WebSiteController(IPublisher publisher, NodeSubsystems[] enabledNodeSubsystems)
            : base(publisher)
        {
            _enabledNodeSubsystems = enabledNodeSubsystems;
            string commonFSRoot = MiniWeb.GetWebRootFileSystemDirectory("EventStore.Web");
            string singleNodeFSRoot = MiniWeb.GetWebRootFileSystemDirectory("EventStore.SingleNode.Web");

            _singleNodeWeb = new MiniWeb("/web", Path.Combine(singleNodeFSRoot, @"", "singlenode-web"));
            _commonWeb = new MiniWeb("/web/es", Path.Combine(commonFSRoot, @"es-common-web"));
        }

        protected override void SubscribeCore(IHttpService service)
        {
            _singleNodeWeb.RegisterControllerActions(service);
            _commonWeb.RegisterControllerActions(service);

            HttpHelpers.RegisterRedirectAction(service, "", "/web/home.htm");
            HttpHelpers.RegisterRedirectAction(service, "/web", "/web/home.htm");

            service.RegisterAction(
                new ControllerAction("/sys/subsystems", HttpMethod.Get, Codec.NoCodecs, new ICodec[] { Codec.Json }),
                OnListNodeSubsystems);
        }

        private void OnListNodeSubsystems(HttpEntityManager http, UriTemplateMatch match)
        {
            http.ReplyTextContent(
            Codec.Json.To(_enabledNodeSubsystems),
            200,
            "OK",
            "application/json",
            null,
            ex => Log.InfoException(ex, "Failed to prepare main menu")
            );
        }
    }
}
