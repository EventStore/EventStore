﻿using System;
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
        private readonly IEventStoreControllerFactory _eventStoreControllerFactory;
        private IList<IEventStoreController> _controllers;
        private readonly IPublisher _networkSendQueue;
        private static readonly ILogger Log = LogManager.GetLoggerFor<PluginsController>();
        private readonly IPluginPublisher _pluginPublisher;
        private static readonly ICodec[] SupportedCodecs = { Codec.Text, Codec.Json, Codec.Xml, Codec.ApplicationXml };

        public PluginsController(IEventStoreControllerFactory eventStoreControllerFactory, IPublisher publisher, IPublisher networkSendQueue, IPluginPublisher pluginPublisher) : base(publisher)
        {
            _eventStoreControllerFactory = eventStoreControllerFactory;
            _networkSendQueue = networkSendQueue;
            _pluginPublisher = pluginPublisher;
        }

        protected override void SubscribeCore(IHttpService service)
        {
            _controllers = _eventStoreControllerFactory.Create();
            foreach (var ctrl in _controllers)
            {
                service.RegisterAction(new ControllerAction(ctrl.StartUriTemplate, HttpMethod.Post, Codec.NoCodecs, SupportedCodecs), OnPostPluginStart);
                service.RegisterAction(new ControllerAction(ctrl.StopUriTemplate, HttpMethod.Post, Codec.NoCodecs, SupportedCodecs), OnPostPluginStop);
                service.RegisterAction(new ControllerAction(ctrl.StatsUriTemplate, HttpMethod.Get, Codec.NoCodecs, SupportedCodecs), OnGetStats);
            }
        }

        private void OnGetStats(HttpEntityManager entity, UriTemplateMatch match)
        {
            var sendToHttpEnvelope = new SendToHttpEnvelope(
                _networkSendQueue, entity, Format.GetPluginStatsCompleted,
                (e, m) => Configure.Ok(e.ResponseCodec.ContentType, Helper.UTF8NoBom, null, null, false));
            Publish(new PluginMessage.GetStats(sendToHttpEnvelope));
        }

        private void OnPostPluginStart(HttpEntityManager entity, UriTemplateMatch match)
        {
            if (entity.User != null && (entity.User.IsInRole(SystemRoles.Admins) || entity.User.IsInRole(SystemRoles.Operations)))
            {
                var serviceType = match.BoundVariables["servicetype"];
                var name = match.BoundVariables["name"];
                Log.Info("Request start a plugin service because Start command has been received.");
                ProcessRequest(entity, new Dictionary<string, dynamic>
                    {
                        {"Name", name},
                        {"ServiceType", serviceType},
                        {"Action", "Start"}
                    });
            }
            else
            {
                entity.ReplyStatus(HttpStatusCode.Unauthorized, "Unauthorized", LogReplyError);
            }
        }

        private void OnPostPluginStop(HttpEntityManager entity, UriTemplateMatch match)
        {
            if (entity.User != null && (entity.User.IsInRole(SystemRoles.Admins) || entity.User.IsInRole(SystemRoles.Operations)))
            {
                var serviceType = match.BoundVariables["servicetype"];
                var name = match.BoundVariables["name"];
                Log.Info("Request stop a plugin service because Stop request has been received.");
                ProcessRequest(entity,
                    new Dictionary<string, dynamic>
                    {
                            {"Name", name},
                            {"ServiceType", serviceType},
                            {"Action", "Stop"}
                    });
            }
            else
            {
                entity.ReplyStatus(HttpStatusCode.Unauthorized, "Unauthorized", LogReplyError);
            }
        }

        private void ProcessRequest(HttpEntityManager entity, IDictionary<string, dynamic> request)
        {
            if (_pluginPublisher.TryPublish(request))
            {
                entity.ReplyStatus(HttpStatusCode.OK, "OK", LogReplyError);
            }
            else
            {
                entity.ReplyStatus(HttpStatusCode.BadRequest, "KO", LogReplyError);
            }
        }

        private void LogReplyError(Exception exc)
        {
            Log.Debug("Error while closing HTTP connection (plugin controller): {0}.", exc.Message);
        }
    }
}
