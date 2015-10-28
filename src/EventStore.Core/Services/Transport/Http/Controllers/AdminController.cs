using System;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services.Transport.Http.Controllers
{
    public class AdminController : CommunicationController
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<AdminController>();

        private static readonly ICodec[] SupportedCodecs = new ICodec[] { Codec.Text, Codec.Json, Codec.Xml, Codec.ApplicationXml };

        public AdminController(IPublisher publisher) : base(publisher)
        {
        }

        protected override void SubscribeCore(IHttpService service)
        {
            service.RegisterAction(new ControllerAction("/admin/shutdown", HttpMethod.Post, Codec.NoCodecs, SupportedCodecs), OnPostShutdown);
            service.RegisterAction(new ControllerAction("/admin/scavenge", HttpMethod.Post, Codec.NoCodecs, SupportedCodecs), OnPostScavenge);
        }

        private void OnPostShutdown(HttpEntityManager entity, UriTemplateMatch match)
        {
            if (entity.User != null && (entity.User.IsInRole(SystemRoles.Admins) || entity.User.IsInRole(SystemRoles.Operations)))
            {
                Log.Info("Request shut down of node because shutdown command has been received.");
                Publish(new ClientMessage.RequestShutdown(exitProcess: true, shutdownHttp: true));
                entity.ReplyStatus(HttpStatusCode.OK, "OK", LogReplyError);
            }
            else
            {
                entity.ReplyStatus(HttpStatusCode.Unauthorized, "Unauthorized", LogReplyError);
            }
        }

        private void OnPostScavenge(HttpEntityManager entity, UriTemplateMatch match)
        {
            if (entity.User != null && (entity.User.IsInRole(SystemRoles.Admins) || entity.User.IsInRole(SystemRoles.Operations)))
            {
                Log.Info("Request scavenging because /admin/scavenge request has been received.");
                Publish(new ClientMessage.ScavengeDatabase(new NoopEnvelope(), Guid.Empty, entity.User));
                entity.ReplyStatus(HttpStatusCode.OK, "OK", LogReplyError);
            }
            else
            {
                entity.ReplyStatus(HttpStatusCode.Unauthorized, "Unauthorized", LogReplyError);
            }
        }

        private void LogReplyError(Exception exc)
        {
            Log.Debug("Error while closing HTTP connection (admin controller): {0}.", exc.Message);
        }
    }
}