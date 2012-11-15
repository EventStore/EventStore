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

        private readonly MiniWeb _miniWebPrelude;


        public ProjectionsController(IPublisher publisher)
            : base(publisher)
        {
            var fileSystemWebRoot = MiniWeb.GetWebRootFileSystemDirectory("EventStore.Projections.Core");
            _miniWebPrelude = new MiniWeb(
                "/web/es/js/projections/v8/Prelude", Path.Combine(fileSystemWebRoot, @"Prelude"));
        }


        protected override void SubscribeCore(IHttpService service, HttpMessagePipe pipe)
        {
            _miniWebPrelude.RegisterControllerActions(service);
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
                    "/projections/onetime", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs, DefaultResponseCodec),
                OnProjectionsGetOneTime);
            service.RegisterControllerAction(
                new ControllerAction(
                    "/projections/adhoc", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs, DefaultResponseCodec),
                OnProjectionsGetAdHoc);
            service.RegisterControllerAction(
                new ControllerAction(
                    "/projections/continuous", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs, DefaultResponseCodec),
                OnProjectionsGetContinuous);
            service.RegisterControllerAction(
                new ControllerAction(
                    "/projections/persistent", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs, DefaultResponseCodec),
                OnProjectionsGetPersistent);
            service.RegisterControllerAction(
                new ControllerAction(
                    "/projections/onetime?name={name}&type={type}", HttpMethod.Post, new ICodec[] {Codec.ManualEncoding},
                    SupportedCodecs, DefaultResponseCodec), OnProjectionsPostAdOneTime);
            service.RegisterControllerAction(
                new ControllerAction(
                    "/projections/adhoc?name={name}&type={type}", HttpMethod.Post, new ICodec[] {Codec.ManualEncoding},
                    SupportedCodecs, DefaultResponseCodec), OnProjectionsPostAdHoc);
            service.RegisterControllerAction(
                new ControllerAction(
                    "/projections/continuous?name={name}&type={type}", HttpMethod.Post,
                    new ICodec[] {Codec.ManualEncoding}, SupportedCodecs, DefaultResponseCodec),
                OnProjectionsPostContinuous);
            service.RegisterControllerAction(
                new ControllerAction(
                    "/projections/persistent?name={name}&type={type}", HttpMethod.Post,
                    new ICodec[] {Codec.ManualEncoding}, SupportedCodecs, DefaultResponseCodec),
                OnProjectionsPostPersistent);

            service.RegisterControllerAction(
                new ControllerAction(
                    "/projection/{name}/query", HttpMethod.Get, Codec.NoCodecs, new ICodec[] {Codec.ManualEncoding},
                    Codec.ManualEncoding), OnProjectionQueryGet);
            service.RegisterControllerAction(
                new ControllerAction(
                    "/projection/{name}/query?type={type}", HttpMethod.Put, new ICodec[] {Codec.ManualEncoding},
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
                    "/projection/{name}/state", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs, DefaultResponseCodec),
                OnProjectionStateGet);
            service.RegisterControllerAction(
                new ControllerAction(
                    "/projection/{name}/state?partition={partition}", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs,
                    DefaultResponseCodec), OnProjectionStateGet);
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
            http.Manager.Reply(
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

        private void OnProjectionsGetOneTime(HttpEntity http, UriTemplateMatch match)
        {
            ProjectionsGet(http, match, ProjectionMode.OneTime);
        }

        private void OnProjectionsGetAdHoc(HttpEntity http, UriTemplateMatch match)
        {
            ProjectionsGet(http, match, ProjectionMode.AdHoc);
        }

        private void OnProjectionsGetContinuous(HttpEntity http, UriTemplateMatch match)
        {
            ProjectionsGet(http, match, ProjectionMode.Continuous);
        }

        private void OnProjectionsGetPersistent(HttpEntity http, UriTemplateMatch match)
        {
            ProjectionsGet(http, match, ProjectionMode.Persistent);
        }

        private void OnProjectionsPostAdOneTime(HttpEntity http, UriTemplateMatch match)
        {
            ProjectionsPost(http, match, ProjectionMode.OneTime, match.BoundVariables["name"]);
        }

        private void OnProjectionsPostAdHoc(HttpEntity http, UriTemplateMatch match)
        {
            ProjectionsPost(http, match, ProjectionMode.AdHoc, match.BoundVariables["name"]);
        }

        private void OnProjectionsPostContinuous(HttpEntity http, UriTemplateMatch match)
        {
            ProjectionsPost(http, match, ProjectionMode.Continuous, match.BoundVariables["name"]);
        }

        private void OnProjectionsPostPersistent(HttpEntity http, UriTemplateMatch match)
        {
            ProjectionsPost(http, match, ProjectionMode.Persistent, match.BoundVariables["name"]);
        }

        private void OnProjectionQueryGet(HttpEntity http, UriTemplateMatch match)
        {
            var envelope = new SendToHttpEnvelope<ProjectionManagementMessage.ProjectionQuery>(
                http, QueryFormatter, QueryConfigurator, ErrorsEnvelope(http));
            Publish(new ProjectionManagementMessage.GetQuery(envelope, match.BoundVariables["name"]));
        }

        private void OnProjectionQueryPut(HttpEntity http, UriTemplateMatch match)
        {
            var envelope = new SendToHttpEnvelope<ProjectionManagementMessage.Updated>(
                http, DefaultFormatter, OkResponseConfigurator, ErrorsEnvelope(http));
            http.Manager.ReadRequestAsync(
                (o, s) =>
                Publish(
                    new ProjectionManagementMessage.UpdateQuery(
                        envelope, match.BoundVariables["name"], match.BoundVariables["type"], s)), Console.WriteLine);
        }

        private void OnProjectionCommandDisable(HttpEntity http, UriTemplateMatch match)
        {
            var envelope = new SendToHttpEnvelope<ProjectionManagementMessage.Updated>(
                http, DefaultFormatter, OkResponseConfigurator, ErrorsEnvelope(http));
            Publish(new ProjectionManagementMessage.Disable(envelope, match.BoundVariables["name"]));
        }

        private void OnProjectionCommandEnable(HttpEntity http, UriTemplateMatch match)
        {
            var envelope = new SendToHttpEnvelope<ProjectionManagementMessage.Updated>(
                http, DefaultFormatter, OkResponseConfigurator, ErrorsEnvelope(http));
            Publish(new ProjectionManagementMessage.Enable(envelope, match.BoundVariables["name"]));
        }

        private void OnProjectionStatusGet(HttpEntity http, UriTemplateMatch match)
        {
            http.Manager.Reply(
                HttpStatusCode.NotImplemented, "Not Implemented",
                e => Log.ErrorException(e, "Error while closing http connection (http service core)"));
        }

        private void OnProjectionDelete(HttpEntity http, UriTemplateMatch match)
        {
            var envelope = new SendToHttpEnvelope<ProjectionManagementMessage.Updated>(
                http, DefaultFormatter, OkResponseConfigurator, ErrorsEnvelope(http));
            Publish(
                new ProjectionManagementMessage.Delete(
                    envelope, match.BoundVariables["name"],
                    "yes".Equals(match.BoundVariables["deleteCheckpointStream"], StringComparison.OrdinalIgnoreCase),
                    "yes".Equals(match.BoundVariables["deleteStateStream"], StringComparison.OrdinalIgnoreCase)));
        }

        private void OnProjectionStatisticsGet(HttpEntity http, UriTemplateMatch match)
        {
            var envelope =
                new SendToHttpWithConversionEnvelope
                    <ProjectionManagementMessage.Statistics, ProjectionsStatisticsHttpFormatted>(
                    http, DefaultFormatter, OkNoCacheResponseConfigurator,
                    status => new ProjectionsStatisticsHttpFormatted(status, s => MakeUrl(match, s)),
                    ErrorsEnvelope(http));
            Publish(new ProjectionManagementMessage.GetStatistics(envelope, null, match.BoundVariables["name"], true));
        }

        private void OnProjectionStateGet(HttpEntity http, UriTemplateMatch match)
        {
            var envelope = new SendToHttpEnvelope<ProjectionManagementMessage.ProjectionState>(
                http, StateFormatter, StateConfigurator, ErrorsEnvelope(http));
            Publish(
                new ProjectionManagementMessage.GetState(
                    envelope, match.BoundVariables["name"], match.BoundVariables["partition"] ?? ""));
        }

        private void OnProjectionDebugGet(HttpEntity http, UriTemplateMatch match)
        {
            var envelope = new SendToHttpEnvelope<ProjectionManagementMessage.ProjectionDebugState>(
                http, DebugStateFormatter, DebugStateConfigurator, ErrorsEnvelope(http));
            Publish(
                new ProjectionManagementMessage.GetDebugState(
                    envelope, match.BoundVariables["name"]));
        }

        private void ProjectionsGet(HttpEntity http, UriTemplateMatch match, ProjectionMode? mode)
        {
            var envelope =
                new SendToHttpWithConversionEnvelope
                    <ProjectionManagementMessage.Statistics, ProjectionsStatisticsHttpFormatted>(
                    http, DefaultFormatter, OkNoCacheResponseConfigurator,
                    status => new ProjectionsStatisticsHttpFormatted(status, s => MakeUrl(match, s)),
                    ErrorsEnvelope(http));
            Publish(new ProjectionManagementMessage.GetStatistics(envelope, mode, null, true));
        }

        private void ProjectionsPost(HttpEntity http, UriTemplateMatch match, ProjectionMode mode, string name)
        {
            var envelope = new SendToHttpEnvelope<ProjectionManagementMessage.Updated>(
                http, DefaultFormatter, (codec, message) =>
                    {
                        var localPath = string.Format("/projection/{0}", message.Name);
                        var url = MakeUrl(match, localPath);
                        return new ResponseConfiguration(
                            201, "Created", codec.ContentType, new KeyValuePair<string, string>("Location", url));
                    }, ErrorsEnvelope(http));
            http.Manager.ReadRequestAsync(
                (o, s) =>
                    {
                        ProjectionManagementMessage.Post postMessage;
                        string handlerType = match.BoundVariables["type"] ?? "JS";
                        if ((mode == ProjectionMode.OneTime || mode == ProjectionMode.AdHoc)
                            && string.IsNullOrEmpty(name))
                            postMessage = new ProjectionManagementMessage.Post(
                                envelope, mode, Guid.NewGuid().ToString("D"), handlerType, s, enabled: true);
                        else
                            postMessage = new ProjectionManagementMessage.Post(
                                envelope, mode, name, handlerType, s, enabled: true);
                        Publish(postMessage);
                    }, Console.WriteLine);
        }

        private ResponseConfiguration StateConfigurator(ICodec codec, ProjectionManagementMessage.ProjectionState state)
        {
            return Configure.OkNoCache("application/json");
        }

        private ResponseConfiguration DebugStateConfigurator(ICodec codec, ProjectionManagementMessage.ProjectionDebugState state)
        {
            return Configure.OkNoCache("application/json");
        }

        private string StateFormatter(ICodec codec, ProjectionManagementMessage.ProjectionState state)
        {
            return state.State;
        }

        private string DebugStateFormatter(ICodec codec, ProjectionManagementMessage.ProjectionDebugState state)
        {
            return state.Events.ToJson();
        }

        private ResponseConfiguration QueryConfigurator(ICodec codec, ProjectionManagementMessage.ProjectionQuery state)
        {
            return Configure.OkNoCache("application/javascript");
        }

        private string QueryFormatter(ICodec codec, ProjectionManagementMessage.ProjectionQuery state)
        {
            return state.Query;
        }

        private ResponseConfiguration OkResponseConfigurator<T>(ICodec codec, T message)
        {
            return new ResponseConfiguration(200, "OK", codec.ContentType);
        }

        private ResponseConfiguration OkNoCacheResponseConfigurator<T>(ICodec codec, T message)
        {
            return Configure.OkNoCache(codec.ContentType);
        }

        private IEnvelope ErrorsEnvelope(HttpEntity http)
        {
            return new SendToHttpEnvelope<ProjectionManagementMessage.NotFound>(
                http, NotFoundFormatter, NotFoundConfigurator,
                new SendToHttpEnvelope<ProjectionManagementMessage.OperationFailed>(
                    http, OperationFailedFormatter, OperationFailedConfigurator, null));
        }

        private ResponseConfiguration NotFoundConfigurator(ICodec codec, ProjectionManagementMessage.NotFound message)
        {
            return new ResponseConfiguration(404, "Not Found", "text/plain");
        }

        private string NotFoundFormatter(ICodec codec, ProjectionManagementMessage.NotFound message)
        {
            return message.Reason;
        }

        private ResponseConfiguration OperationFailedConfigurator(
            ICodec codec, ProjectionManagementMessage.OperationFailed message)
        {
            return new ResponseConfiguration(500, "Failed", "text/plain");
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

/*
        private void OnPostShutdown(HttpEntity entity, UriTemplateMatch match)
        {
            Publish(new ClientMessage.RequestShutdown());
            entity.Manager.Reply(HttpStatusCode.OK,
                                 "OK",
                                 (s, e) => Log.ErrorException(e, "Error while closing http connection (admin controller)"));
        }
*/
    }
}
