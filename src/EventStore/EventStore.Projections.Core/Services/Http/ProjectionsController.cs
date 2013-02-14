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
using System.Collections.Generic;
using System.IO;
using System.Text;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.Services.Transport.Http.Codecs;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Core.Util;
using EventStore.Projections.Core.Messages;
using EventStore.Transport.Http;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Projections.Core.Services.Http
{
    public class ProjectionsController : CommunicationController
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<ProjectionsController>();

        private static readonly ICodec[] SupportedCodecs = new ICodec[] {Codec.Json};
        private static readonly ICodec DefaultResponseCodec = Codec.Json;

        private readonly MiniWeb _singleNodeJs;
        private readonly MiniWeb _miniWebPrelude;
        private readonly MiniWeb _miniWebResources;
        private readonly IPublisher _networkSendQueue;

        public ProjectionsController(IPublisher publisher, IPublisher networkSendQueue)
            : base(publisher)
        {
            var singleNodeFSRoot = MiniWeb.GetWebRootFileSystemDirectory("EventStore.SingleNode.Web");
            _singleNodeJs = new MiniWeb("/web/es/js/projections", Path.Combine(singleNodeFSRoot, Path.Combine("singlenode-web", "js", "projections")));
            
            var fileSystemWebRoot = MiniWeb.GetWebRootFileSystemDirectory("EventStore.Projections.Core");
            _networkSendQueue = networkSendQueue;
            _miniWebPrelude = new MiniWeb(
                "/web/es/js/projections/v8/Prelude", Path.Combine(fileSystemWebRoot, @"Prelude"));

            _miniWebResources = new MiniWeb(
                "/web/es/js/projections/resources", Path.Combine(fileSystemWebRoot, Path.Combine("web-resources", "js")));
        }
        
        
        protected override void SubscribeCore(IHttpService service, HttpMessagePipe pipe)
        {
            _singleNodeJs.RegisterControllerActions(service);

            _miniWebPrelude.RegisterControllerActions(service);
            _miniWebResources.RegisterControllerActions(service);
            
            HttpHelpers.RegisterRedirectAction(service, "/web/projections", "/web/projections.htm");

            service.RegisterControllerAction(
                new ControllerAction(
                    "/projections", HttpMethod.Get, Codec.NoCodecs, new ICodec[] {Codec.ManualEncoding},
                    Codec.ManualEncoding), OnProjections);
            service.RegisterControllerAction(
                new ControllerAction(
                    "/projections/any", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs, DefaultResponseCodec),
                OnProjectionsGetAny);
            service.RegisterControllerAction(
                new ControllerAction(
                    "/projections/all-non-transient", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs, DefaultResponseCodec),
                OnProjectionsGetAllNonTransient);
            service.RegisterControllerAction(
                new ControllerAction(
                    "/projections/transient", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs, DefaultResponseCodec),
                OnProjectionsGetTransient);
            service.RegisterControllerAction(
                new ControllerAction(
                    "/projections/onetime", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs, DefaultResponseCodec),
                OnProjectionsGetOneTime);
            service.RegisterControllerAction(
                new ControllerAction(
                    "/projections/continuous", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs, DefaultResponseCodec),
                OnProjectionsGetContinuous);
            service.RegisterControllerAction(
                new ControllerAction(
                    "/projections/transient?name={name}&type={type}&enabled={enabled}",
                    HttpMethod.Post, new ICodec[] {Codec.ManualEncoding}, SupportedCodecs, DefaultResponseCodec),
                OnProjectionsPostTransient);
            service.RegisterControllerAction(
                new ControllerAction(
                    "/projections/onetime?name={name}&type={type}&enabled={enabled}&checkpoints={checkpoints}&emit={emit}",
                    HttpMethod.Post, new ICodec[] { Codec.ManualEncoding }, SupportedCodecs, DefaultResponseCodec),
                OnProjectionsPostOneTime);
            service.RegisterControllerAction(
                new ControllerAction(
                    "/projections/continuous?name={name}&type={type}&enabled={enabled}&emit={emit}", HttpMethod.Post,
                    new ICodec[] {Codec.ManualEncoding}, SupportedCodecs, DefaultResponseCodec),
                OnProjectionsPostContinuous);
            service.RegisterControllerAction(
                new ControllerAction(
                    "/projection/{name}/query?config={config}", HttpMethod.Get, Codec.NoCodecs, new ICodec[] {Codec.ManualEncoding},
                    Codec.ManualEncoding), OnProjectionQueryGet);
            service.RegisterControllerAction(
                new ControllerAction(
                    "/projection/{name}/query?type={type}&emit={emit}", HttpMethod.Put, new ICodec[] { Codec.ManualEncoding },
                    SupportedCodecs, DefaultResponseCodec), OnProjectionQueryPut);
            service.RegisterControllerAction(
                new ControllerAction(
                    "/projection/{name}", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs, DefaultResponseCodec),
                OnProjectionStatusGet);
            service.RegisterControllerAction(
                new ControllerAction(
                    "/projection/{name}?deleteStateStream={deleteStateStream}&deleteCheckpointStream={deleteCheckpointStream}",
                    HttpMethod.Delete, new ICodec[] {Codec.ManualEncoding}, SupportedCodecs, DefaultResponseCodec),
                OnProjectionDelete);
            service.RegisterControllerAction(
                new ControllerAction(
                    "/projection/{name}/statistics", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs,
                    DefaultResponseCodec), OnProjectionStatisticsGet);
            service.RegisterControllerAction(
                new ControllerAction(
                    "/projection/{name}/debug", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs, DefaultResponseCodec),
                OnProjectionDebugGet);
            service.RegisterControllerAction(
                new ControllerAction(
                    "/projection/{name}/state?partition={partition}", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs,
                    DefaultResponseCodec), OnProjectionStateGet);
            service.RegisterControllerAction(
                new ControllerAction(
                    "/projection/{name}/result?partition={partition}", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs,
                    DefaultResponseCodec), OnProjectionResultGet);
            service.RegisterControllerAction(
                new ControllerAction(
                    "/projection/{name}/command/disable", HttpMethod.Post, new ICodec[] {Codec.ManualEncoding},
                    SupportedCodecs, DefaultResponseCodec), OnProjectionCommandDisable);
            service.RegisterControllerAction(
                new ControllerAction(
                    "/projection/{name}/command/enable", HttpMethod.Post, new ICodec[] {Codec.ManualEncoding},
                    SupportedCodecs, DefaultResponseCodec), OnProjectionCommandEnable);
        }

        private static void OnProjections(HttpEntity http, UriTemplateMatch match)
        {
            http.Manager.ReplyTextContent(
                "Moved", 302, "Found", "text/plain",
                new[]
                    {
                        new KeyValuePair<string, string>(
                    "Location", new Uri(match.BaseUri, "/web/projections.htm").AbsoluteUri)
                    }, Console.WriteLine);
        }

        private void OnProjectionsGetAny(HttpEntity http, UriTemplateMatch match)
        {
            ProjectionsGet(http, match, null);
        }

        private void OnProjectionsGetAllNonTransient(HttpEntity http, UriTemplateMatch match)
        {
            ProjectionsGet(http, match, ProjectionMode.AllNonTransient);
        }

        private void OnProjectionsGetTransient(HttpEntity http, UriTemplateMatch match)
        {
            ProjectionsGet(http, match, ProjectionMode.Transient);
        }

        private void OnProjectionsGetOneTime(HttpEntity http, UriTemplateMatch match)
        {
            ProjectionsGet(http, match, ProjectionMode.OneTime);
        }

        private void OnProjectionsGetContinuous(HttpEntity http, UriTemplateMatch match)
        {
            ProjectionsGet(http, match, ProjectionMode.Continuous);
        }

        private void OnProjectionsPostTransient(HttpEntity http, UriTemplateMatch match)
        {
            ProjectionsPost(http, match, ProjectionMode.Transient, match.BoundVariables["name"]);
        }

        private void OnProjectionsPostOneTime(HttpEntity http, UriTemplateMatch match)
        {
            ProjectionsPost(http, match, ProjectionMode.OneTime, match.BoundVariables["name"]);
        }

        private void OnProjectionsPostContinuous(HttpEntity http, UriTemplateMatch match)
        {
            ProjectionsPost(http, match, ProjectionMode.Continuous, match.BoundVariables["name"]);
        }

        private void OnProjectionQueryGet(HttpEntity http, UriTemplateMatch match)
        {
            SendToHttpEnvelope<ProjectionManagementMessage.ProjectionQuery> envelope;
            var withConfig = IsOn(match, "config", false);
            if (withConfig)
                envelope = new SendToHttpEnvelope<ProjectionManagementMessage.ProjectionQuery>(
                    _networkSendQueue, http, QueryConfigFormatter, QueryConfigConfigurator, ErrorsEnvelope(http));
            else
                envelope = new SendToHttpEnvelope<ProjectionManagementMessage.ProjectionQuery>(
                    _networkSendQueue, http, QueryFormatter, QueryConfigurator, ErrorsEnvelope(http));
            Publish(new ProjectionManagementMessage.GetQuery(envelope, match.BoundVariables["name"]));
        }

        private void OnProjectionQueryPut(HttpEntity http, UriTemplateMatch match)
        {
            var envelope = new SendToHttpEnvelope<ProjectionManagementMessage.Updated>(
                _networkSendQueue, http, DefaultFormatter, OkResponseConfigurator, ErrorsEnvelope(http));
            var emitEnabled = IsOn(match, "emit", null);
            http.Manager.ReadTextRequestAsync(
                (o, s) =>
                Publish(
                    new ProjectionManagementMessage.UpdateQuery(
                        envelope, match.BoundVariables["name"], match.BoundVariables["type"], s, emitEnabled: emitEnabled)), Console.WriteLine);
        }

        private void OnProjectionCommandDisable(HttpEntity http, UriTemplateMatch match)
        {
            var envelope = new SendToHttpEnvelope<ProjectionManagementMessage.Updated>(
                _networkSendQueue, http, DefaultFormatter, OkResponseConfigurator, ErrorsEnvelope(http));
            Publish(new ProjectionManagementMessage.Disable(envelope, match.BoundVariables["name"]));
        }

        private void OnProjectionCommandEnable(HttpEntity http, UriTemplateMatch match)
        {
            var envelope = new SendToHttpEnvelope<ProjectionManagementMessage.Updated>(
                _networkSendQueue, http, DefaultFormatter, OkResponseConfigurator, ErrorsEnvelope(http));
            Publish(new ProjectionManagementMessage.Enable(envelope, match.BoundVariables["name"]));
        }

        private void OnProjectionStatusGet(HttpEntity http, UriTemplateMatch match)
        {
            http.Manager.ReplyStatus(
                HttpStatusCode.NotImplemented, "Not Implemented",
                e => Log.ErrorException(e, "Error while closing http connection (http service core)"));
        }

        private void OnProjectionDelete(HttpEntity http, UriTemplateMatch match)
        {
            var envelope = new SendToHttpEnvelope<ProjectionManagementMessage.Updated>(
                _networkSendQueue, http, DefaultFormatter, OkResponseConfigurator, ErrorsEnvelope(http));
            Publish(
                new ProjectionManagementMessage.Delete(
                    envelope, match.BoundVariables["name"],
                    IsOn(match, "deleteCheckpointStream", false),
                    IsOn(match, "deleteStateStream", false)));
        }

        private void OnProjectionStatisticsGet(HttpEntity http, UriTemplateMatch match)
        {
            var envelope =
                new SendToHttpWithConversionEnvelope
                    <ProjectionManagementMessage.Statistics, ProjectionsStatisticsHttpFormatted>(
                    _networkSendQueue, http, DefaultFormatter, OkNoCacheResponseConfigurator,
                    status => new ProjectionsStatisticsHttpFormatted(status, s => MakeUrl(match, s)),
                    ErrorsEnvelope(http));
            Publish(new ProjectionManagementMessage.GetStatistics(envelope, null, match.BoundVariables["name"], true));
        }

        private void OnProjectionStateGet(HttpEntity http, UriTemplateMatch match)
        {
            var envelope = new SendToHttpEnvelope<ProjectionManagementMessage.ProjectionState>(
                _networkSendQueue, http, StateFormatter, StateConfigurator, ErrorsEnvelope(http));
            Publish(
                new ProjectionManagementMessage.GetState(
                    envelope, match.BoundVariables["name"], match.BoundVariables["partition"] ?? ""));
        }

        private void OnProjectionResultGet(HttpEntity http, UriTemplateMatch match)
        {
            var envelope = new SendToHttpEnvelope<ProjectionManagementMessage.ProjectionResult>(
                _networkSendQueue, http, ResultFormatter, ResultConfigurator, ErrorsEnvelope(http));
            Publish(
                new ProjectionManagementMessage.GetResult(
                    envelope, match.BoundVariables["name"], match.BoundVariables["partition"] ?? ""));
        }

        private void OnProjectionDebugGet(HttpEntity http, UriTemplateMatch match)
        {
            var envelope = new SendToHttpEnvelope<ProjectionManagementMessage.ProjectionDebugState>(
                _networkSendQueue, http, DebugStateFormatter, DebugStateConfigurator, ErrorsEnvelope(http));
            Publish(
                new ProjectionManagementMessage.GetDebugState(
                    envelope, match.BoundVariables["name"]));
        }

        private void ProjectionsGet(HttpEntity http, UriTemplateMatch match, ProjectionMode? mode)
        {
            var envelope =
                new SendToHttpWithConversionEnvelope<ProjectionManagementMessage.Statistics, ProjectionsStatisticsHttpFormatted>(
                    _networkSendQueue, http, DefaultFormatter, OkNoCacheResponseConfigurator,
                    status => new ProjectionsStatisticsHttpFormatted(status, s => MakeUrl(match, s)),
                    ErrorsEnvelope(http));
            Publish(new ProjectionManagementMessage.GetStatistics(envelope, mode, null, true));
        }

        private void ProjectionsPost(HttpEntity http, UriTemplateMatch match, ProjectionMode mode, string name)
        {
            var envelope = new SendToHttpEnvelope<ProjectionManagementMessage.Updated>(
                _networkSendQueue, http, DefaultFormatter, (codec, message) =>
                    {
                        var localPath = string.Format("/projection/{0}", message.Name);
                        var url = MakeUrl(match, localPath);
                        return new ResponseConfiguration(
                            201, "Created", codec.ContentType, codec.Encoding, new KeyValuePair<string, string>("Location", url));
                    }, ErrorsEnvelope(http));
            http.Manager.ReadTextRequestAsync(
                (o, s) =>
                    {
                        ProjectionManagementMessage.Post postMessage;
                        string handlerType = match.BoundVariables["type"] ?? "JS";
                        bool emitEnabled = IsOn(match, "emit", false);
                        bool checkpointsEnabled = mode >= ProjectionMode.Continuous
                                                      ? true
                                                      : IsOn(match, "checkpoints", false);
                        bool enabled = IsOn(match, "enabled", def: true);
                        if (mode <= ProjectionMode.OneTime && string.IsNullOrEmpty(name))
                            postMessage = new ProjectionManagementMessage.Post(
                                envelope, mode, Guid.NewGuid().ToString("D"), handlerType, s, enabled: enabled,
                                checkpointsEnabled: checkpointsEnabled, emitEnabled: emitEnabled);
                        else
                            postMessage = new ProjectionManagementMessage.Post(
                                envelope, mode, name, handlerType, s, enabled: enabled,
                                checkpointsEnabled: checkpointsEnabled, emitEnabled: emitEnabled);
                        Publish(postMessage);
                    }, Console.WriteLine);
        }

        private ResponseConfiguration StateConfigurator(ICodec codec, ProjectionManagementMessage.ProjectionState state)
        {
            if (state.Exception != null)
                return Configure.InternalServerError();
            else
                return Configure.OkNoCache("application/json", Encoding.UTF8);
        }

        private ResponseConfiguration ResultConfigurator(ICodec codec, ProjectionManagementMessage.ProjectionResult state)
        {
            if (state.Exception != null)
                return Configure.InternalServerError();
            else
                return Configure.OkNoCache("application/json", Encoding.UTF8);
        }

        private ResponseConfiguration DebugStateConfigurator(ICodec codec, ProjectionManagementMessage.ProjectionDebugState state)
        {
            return Configure.OkNoCache("application/json", Encoding.UTF8);
        }

        private string StateFormatter(ICodec codec, ProjectionManagementMessage.ProjectionState state)
        {
            if (state.Exception != null)
                return state.Exception.ToString();
            else
                return state.State;
        }

        private string ResultFormatter(ICodec codec, ProjectionManagementMessage.ProjectionResult state)
        {
            if (state.Exception != null)
                return state.Exception.ToString();
            else
                return state.Result;
        }

        private string DebugStateFormatter(ICodec codec, ProjectionManagementMessage.ProjectionDebugState state)
        {
            return state.Events.ToJson();
        }

        private ResponseConfiguration QueryConfigurator(ICodec codec, ProjectionManagementMessage.ProjectionQuery state)
        {
            return Configure.OkNoCache("application/javascript", Encoding.UTF8);
        }

        private string QueryFormatter(ICodec codec, ProjectionManagementMessage.ProjectionQuery state)
        {
            return state.Query;
        }

        private string QueryConfigFormatter(ICodec codec, ProjectionManagementMessage.ProjectionQuery state)
        {
            return state.ToJson();
        }

        private ResponseConfiguration QueryConfigConfigurator(ICodec codec, ProjectionManagementMessage.ProjectionQuery state)
        {
            return Configure.OkNoCache("application/json", Encoding.UTF8);
        }

        private ResponseConfiguration OkResponseConfigurator<T>(ICodec codec, T message)
        {
            return new ResponseConfiguration(200, "OK", codec.ContentType, Encoding.UTF8);
        }

        private ResponseConfiguration OkNoCacheResponseConfigurator<T>(ICodec codec, T message)
        {
            return Configure.OkNoCache(codec.ContentType, codec.Encoding);
        }

        private IEnvelope ErrorsEnvelope(HttpEntity http)
        {
            return new SendToHttpEnvelope<ProjectionManagementMessage.NotFound>(
                _networkSendQueue, http, NotFoundFormatter, NotFoundConfigurator,
                new SendToHttpEnvelope<ProjectionManagementMessage.OperationFailed>(
                    _networkSendQueue, http, OperationFailedFormatter, OperationFailedConfigurator, null));
        }

        private ResponseConfiguration NotFoundConfigurator(ICodec codec, ProjectionManagementMessage.NotFound message)
        {
            return new ResponseConfiguration(404, "Not Found", "text/plain", Encoding.UTF8);
        }

        private string NotFoundFormatter(ICodec codec, ProjectionManagementMessage.NotFound message)
        {
            return message.Reason;
        }

        private ResponseConfiguration OperationFailedConfigurator(
            ICodec codec, ProjectionManagementMessage.OperationFailed message)
        {
            return new ResponseConfiguration(500, "Failed", "text/plain", Encoding.UTF8);
        }

        private string OperationFailedFormatter(ICodec codec, ProjectionManagementMessage.OperationFailed message)
        {
            return message.Reason;
        }

        private static string MakeUrl(UriTemplateMatch match, string localPath)
        {
            return new Uri(match.BaseUri, localPath).AbsoluteUri;
        }

        private static string DefaultFormatter<T>(ICodec codec, T message)
        {
            return codec.To(message);
        }

        private static bool? IsOn(UriTemplateMatch match, string option, bool? def)
        {
            var rawValue = match.BoundVariables[option];
            if (string.IsNullOrEmpty(rawValue))
                return def;
            var value = rawValue.ToLowerInvariant();
            if ("yes" == value || "true" == value || "1" == value)
                return true;
            if ("no" == value || "false" == value || "0" == value)
                return false;
            //TODO: throw?
            return def;
        }

        private static bool IsOn(UriTemplateMatch match, string option, bool def)
        {
            var rawValue = match.BoundVariables[option];
            if (string.IsNullOrEmpty(rawValue))
                return def;
            var value = rawValue.ToLowerInvariant();
            return "yes" == value || "true" == value || "1" == value;
        }


/*
        private void OnPostShutdown(HttpEntity entity, UriTemplateMatch match)
        {
            Publish(new ClientMessage.RequestShutdown());
            entity.Manager.ReplyStatus(HttpStatusCode.OK,
                                 "OK",
                                 (s, e) => Log.ErrorException(e, "Error while closing http connection (admin controller)"));
        }
*/
    }
}
