using System;
using System.Collections.Generic;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.Plugins;
using EventStore.Plugins;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services.Transport.Http.Controllers
{
    public class PluginsController : CommunicationController
    {
        private readonly IPublisher _networkSendQueue;
        private readonly IEventStoreControllerFactory _eventStoreControllerFactory;
        private static readonly ILogger Log = LogManager.GetLoggerFor<PluginsController>();

        private static readonly ICodec[] SupportedCodecs = new ICodec[] { Codec.Text, Codec.Json, Codec.Xml, Codec.ApplicationXml };

        public PluginsController(IPublisher publisher, IPublisher networkSendQueue, IEventStoreControllerFactory eventStoreControllerFactory) : base(publisher)
        {
            _networkSendQueue = networkSendQueue;
            _eventStoreControllerFactory = eventStoreControllerFactory;
        }

        protected override void SubscribeCore(IHttpService service)
        {
            var controllers = _eventStoreControllerFactory.Create();
            foreach (var eventStoreController in controllers)
            {
                foreach (var registeredAction in eventStoreController.RegisteredActions)
                {
                    service.RegisterAction(new ControllerAction(registeredAction.Key, HttpMethod.Post, Codec.NoCodecs, SupportedCodecs), OnRequest);
                }
            }
        }

        private void OnRequest(HttpEntityManager entity, UriTemplateMatch match)
        {
            if (entity.User != null && (entity.User.IsInRole(SystemRoles.Admins) || entity.User.IsInRole(SystemRoles.Operations)))
            {
                var serviceType = match.BoundVariables["servicetype"];
                
            }
            else
            {
                entity.ReplyStatus(HttpStatusCode.Unauthorized, "Unauthorized", LogReplyError);
            }
        }

        private void LogReplyError(Exception exc)
        {
            Log.Debug("Error while using HTTP connection: {0}.", exc.GetBaseException().Message);
        }
    }
}