using System;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Messages;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services.Transport.Http.Controllers
{
    public class InfoController : IHttpController
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<InfoController>();

        private static readonly ICodec[] SupportedCodecs = new ICodec[] { Codec.Json, Codec.Xml, Codec.ApplicationXml, Codec.Text };

        public void Subscribe(IHttpService service)
        {
            Ensure.NotNull(service, "service");
            service.RegisterAction(new ControllerAction("/info", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs), OnGetInfo);
        }

        private void OnGetInfo(HttpEntityManager entity, UriTemplateMatch match)
        {
            entity.ReplyTextContent(Codec.Json.To(new
                                    {
                                        ESVersion = VersionInfo.Version
                                    }),
                                    HttpStatusCode.OK,
                                    "OK",
                                    entity.ResponseCodec.ContentType,
                                    null,
                                    e => Log.ErrorException(e, "Error while writing http response (info)"));
        }
    }
}