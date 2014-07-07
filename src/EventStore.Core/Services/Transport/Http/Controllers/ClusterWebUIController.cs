using System;
using System.Collections.Generic;
using System.IO;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Util;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services.Transport.Http.Controllers
{
    public class ClusterWebUIController : CommunicationController
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<ClusterWebUIController>();

        private readonly NodeSubsystems[] _enabledNodeSubsystems;

        private readonly MiniWeb _commonWeb;
        private readonly MiniWeb _clusterNodeWeb;

        public ClusterWebUIController(IPublisher publisher, NodeSubsystems[] enabledNodeSubsystems)
            : base(publisher)
        {
            _enabledNodeSubsystems = enabledNodeSubsystems;
        
            string commonFSRoot = MiniWeb.GetWebRootFileSystemDirectory("EventStore.Web");
            //string clusterNodeFSRoot = MiniWeb.GetWebRootFileSystemDirectory("../../../Cluster/src/EventStore.ClusterNode.Web");
            string clusterNodeFSRoot = MiniWeb.GetWebRootFileSystemDirectory();

            _clusterNodeWeb = new MiniWeb("/web", Path.Combine(clusterNodeFSRoot, @"clusternode-web"));
            _commonWeb = new MiniWeb("/web/es", Path.Combine(commonFSRoot, @"es-common-web"));
        }

        protected override void SubscribeCore(IHttpService service)
        {
            _clusterNodeWeb.RegisterControllerActions(service);
            _commonWeb.RegisterControllerActions(service);
            RegisterRedirectAction(service, "", "/web/home.htm");
            RegisterRedirectAction(service, "/web", "/web/home.htm");

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

        private static void RegisterRedirectAction(IHttpService service, string fromUrl, string toUrl)
        {
            service.RegisterAction(
                new ControllerAction(
                    fromUrl, 
                    HttpMethod.Get, 
                    Codec.NoCodecs, 
                    new ICodec[] { Codec.ManualEncoding }),
                    (http, match) => http.ReplyTextContent(
                        "Moved", 302, "Found", "text/plain",
                        new[]
                            {
                                new KeyValuePair<string, string>(
                                    "Location",   new Uri(match.BaseUri, toUrl).AbsoluteUri)
                            }, Console.WriteLine));
        }
    }
}
