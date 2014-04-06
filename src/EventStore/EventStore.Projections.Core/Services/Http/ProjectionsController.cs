using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Core.Util;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.EventReaders.Feeds;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;
using Newtonsoft.Json.Linq;

namespace EventStore.Projections.Core.Services.Http
{
    public class ProjectionsController : CommunicationController
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<ProjectionsController>();

        private static readonly ICodec[] SupportedCodecs = new ICodec[] {Codec.Json};

        private readonly MiniWeb _singleNodeJs;
        private readonly MiniWeb _miniWebPrelude;
        private readonly MiniWeb _miniWebResources;
        private readonly IHttpForwarder _httpForwarder;
        private readonly IPublisher _networkSendQueue;

        public ProjectionsController(IHttpForwarder httpForwarder, IPublisher publisher, IPublisher networkSendQueue)
            : base(publisher)
        {
            _httpForwarder = httpForwarder;

            var singleNodeFSRoot = MiniWeb.GetWebRootFileSystemDirectory("EventStore.SingleNode.Web");
            _singleNodeJs = new MiniWeb("/web/es/js/projections", Path.Combine(singleNodeFSRoot, Path.Combine("singlenode-web", "js", "projections")));

            var fileSystemWebRoot = MiniWeb.GetWebRootFileSystemDirectory("EventStore.Projections.Core");
            _networkSendQueue = networkSendQueue;
            _miniWebPrelude = new MiniWeb("/web/es/js/projections/v8/Prelude", Path.Combine(fileSystemWebRoot, @"Prelude"));
            _miniWebResources = new MiniWeb("/web/es/js/projections/resources", Path.Combine(fileSystemWebRoot, Path.Combine("web-resources", "js")));
        }

        protected override void SubscribeCore(IHttpService service)
        {
            _singleNodeJs.RegisterControllerActions(service);

            _miniWebPrelude.RegisterControllerActions(service);
            _miniWebResources.RegisterControllerActions(service);

            HttpHelpers.RegisterRedirectAction(service, "/web/projections", "/web/projections.htm");

            Register(service, "/projections",
                     HttpMethod.Get, OnProjections, Codec.NoCodecs, new ICodec[] {Codec.ManualEncoding});
            Register(service, "/projections/any",
                     HttpMethod.Get, OnProjectionsGetAny, Codec.NoCodecs, SupportedCodecs);
            Register(service, "/projections/all-non-transient",
                     HttpMethod.Get, OnProjectionsGetAllNonTransient, Codec.NoCodecs, SupportedCodecs);
            Register(service, "/projections/transient",
                     HttpMethod.Get, OnProjectionsGetTransient, Codec.NoCodecs, SupportedCodecs);
            Register(service, "/projections/onetime",
                     HttpMethod.Get, OnProjectionsGetOneTime, Codec.NoCodecs, SupportedCodecs);
            Register(service, "/projections/continuous",
                     HttpMethod.Get, OnProjectionsGetContinuous, Codec.NoCodecs, SupportedCodecs);
            Register(service, "/projections/transient?name={name}&type={type}&enabled={enabled}",
                     HttpMethod.Post, OnProjectionsPostTransient, new ICodec[] {Codec.ManualEncoding}, SupportedCodecs);
            Register(service, "/projections/onetime?name={name}&type={type}&enabled={enabled}&checkpoints={checkpoints}&emit={emit}",
                     HttpMethod.Post, OnProjectionsPostOneTime, new ICodec[] {Codec.ManualEncoding}, SupportedCodecs);
            Register(service, "/projections/continuous?name={name}&type={type}&enabled={enabled}&emit={emit}",
                     HttpMethod.Post, OnProjectionsPostContinuous, new ICodec[] {Codec.ManualEncoding}, SupportedCodecs);
            Register(service, "/projection/{name}/query?config={config}",
                     HttpMethod.Get, OnProjectionQueryGet, Codec.NoCodecs, new ICodec[] {Codec.ManualEncoding});
            Register(service, "/projection/{name}/query?type={type}&emit={emit}",
                     HttpMethod.Put, OnProjectionQueryPut, new ICodec[] {Codec.ManualEncoding}, SupportedCodecs);
            Register(service, "/projection/{name}",
                     HttpMethod.Get, OnProjectionStatusGet, Codec.NoCodecs, SupportedCodecs);
            Register(service, "/projection/{name}?deleteStateStream={deleteStateStream}&deleteCheckpointStream={deleteCheckpointStream}",
                     HttpMethod.Delete, OnProjectionDelete, Codec.NoCodecs, SupportedCodecs);
            Register(service, "/projection/{name}/statistics",
                     HttpMethod.Get, OnProjectionStatisticsGet, Codec.NoCodecs, SupportedCodecs);
            Register(service, "/projections/read-events",
                     HttpMethod.Post, OnProjectionsReadEvents, SupportedCodecs, SupportedCodecs);
            Register(service, "/projection/{name}/state?partition={partition}",
                     HttpMethod.Get, OnProjectionStateGet, Codec.NoCodecs, SupportedCodecs);
            Register(service, "/projection/{name}/result?partition={partition}",
                     HttpMethod.Get, OnProjectionResultGet, Codec.NoCodecs, SupportedCodecs);
            Register(service, "/projection/{name}/command/disable?enableRunAs={enableRunAs}",
                     HttpMethod.Post, OnProjectionCommandDisable, Codec.NoCodecs, SupportedCodecs);
            Register(service, "/projection/{name}/command/enable?enableRunAs={enableRunAs}",
                     HttpMethod.Post, OnProjectionCommandEnable, Codec.NoCodecs, SupportedCodecs);
            Register(service, "/projection/{name}/command/reset?enableRunAs={enableRunAs}",
                     HttpMethod.Post, OnProjectionCommandReset, Codec.NoCodecs, SupportedCodecs);
            Register(service, "/projection/{name}/command/abort?enableRunAs={enableRunAs}",
                     HttpMethod.Post, OnProjectionCommandAbort, Codec.NoCodecs, SupportedCodecs);
        }

        private void OnProjections(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;

            http.ReplyTextContent(
                "Moved", 302, "Found", "text/plain",
                new[]
                    {
                        new KeyValuePair<string, string>(
                    "Location", new Uri(match.BaseUri, "/web/projections.htm").AbsoluteUri)
                    }, x => Log.DebugException(x, "Reply Text Content Failed."));
        }

        private void OnProjectionsGetAny(HttpEntityManager http, UriTemplateMatch match)
        {
            ProjectionsGet(http, match, null);
        }

        private void OnProjectionsGetAllNonTransient(HttpEntityManager http, UriTemplateMatch match)
        {
            ProjectionsGet(http, match, ProjectionMode.AllNonTransient);
        }

        private void OnProjectionsGetTransient(HttpEntityManager http, UriTemplateMatch match)
        {
            ProjectionsGet(http, match, ProjectionMode.Transient);
        }

        private void OnProjectionsGetOneTime(HttpEntityManager http, UriTemplateMatch match)
        {
            ProjectionsGet(http, match, ProjectionMode.OneTime);
        }

        private void OnProjectionsGetContinuous(HttpEntityManager http, UriTemplateMatch match)
        {
            ProjectionsGet(http, match, ProjectionMode.Continuous);
        }

        private void OnProjectionsPostTransient(HttpEntityManager http, UriTemplateMatch match)
        {
            ProjectionsPost(http, match, ProjectionMode.Transient, match.BoundVariables["name"]);
        }

        private void OnProjectionsPostOneTime(HttpEntityManager http, UriTemplateMatch match)
        {
            ProjectionsPost(http, match, ProjectionMode.OneTime, match.BoundVariables["name"]);
        }

        private void OnProjectionsPostContinuous(HttpEntityManager http, UriTemplateMatch match)
        {
            ProjectionsPost(http, match, ProjectionMode.Continuous, match.BoundVariables["name"]);
        }

        private void OnProjectionQueryGet(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;

            SendToHttpEnvelope<ProjectionManagementMessage.ProjectionQuery> envelope;
            var withConfig = IsOn(match, "config", false);
            if (withConfig)
                envelope = new SendToHttpEnvelope<ProjectionManagementMessage.ProjectionQuery>(
                    _networkSendQueue, http, QueryConfigFormatter, QueryConfigConfigurator, ErrorsEnvelope(http));
            else
                envelope = new SendToHttpEnvelope<ProjectionManagementMessage.ProjectionQuery>(
                    _networkSendQueue, http, QueryFormatter, QueryConfigurator, ErrorsEnvelope(http));
            Publish(new ProjectionManagementMessage.GetQuery(envelope, match.BoundVariables["name"], GetRunAs(http, match)));
        }

        private void OnProjectionQueryPut(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;

            var envelope = new SendToHttpEnvelope<ProjectionManagementMessage.Updated>(
                _networkSendQueue, http, DefaultFormatter, OkResponseConfigurator, ErrorsEnvelope(http));
            var emitEnabled = IsOn(match, "emit", null);
            http.ReadTextRequestAsync(
                (o, s) =>
                Publish(
                    new ProjectionManagementMessage.UpdateQuery(
                        envelope, match.BoundVariables["name"], GetRunAs(http, match), match.BoundVariables["type"], s, emitEnabled: emitEnabled)), Console.WriteLine);
        }

        private void OnProjectionCommandDisable(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;

            var envelope = new SendToHttpEnvelope<ProjectionManagementMessage.Updated>(
                _networkSendQueue, http, DefaultFormatter, OkResponseConfigurator, ErrorsEnvelope(http));
            Publish(new ProjectionManagementMessage.Disable(envelope, match.BoundVariables["name"], GetRunAs(http, match)));
        }

        private void OnProjectionCommandEnable(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;

            var envelope = new SendToHttpEnvelope<ProjectionManagementMessage.Updated>(
                _networkSendQueue, http, DefaultFormatter, OkResponseConfigurator, ErrorsEnvelope(http));
            var name = match.BoundVariables["name"];
            Publish(new ProjectionManagementMessage.Enable(envelope, name, GetRunAs(http, match)));
        }

        private void OnProjectionCommandReset(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;

            var envelope = new SendToHttpEnvelope<ProjectionManagementMessage.Updated>(
                _networkSendQueue, http, DefaultFormatter, OkResponseConfigurator, ErrorsEnvelope(http));
            Publish(new ProjectionManagementMessage.Reset(envelope, match.BoundVariables["name"], GetRunAs(http, match)));
        }

        private void OnProjectionCommandAbort(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;

            var envelope = new SendToHttpEnvelope<ProjectionManagementMessage.Updated>(
                _networkSendQueue, http, DefaultFormatter, OkResponseConfigurator, ErrorsEnvelope(http));
            Publish(new ProjectionManagementMessage.Abort(envelope, match.BoundVariables["name"], GetRunAs(http, match)));
        }

        private void OnProjectionStatusGet(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;

            var envelope =
                new SendToHttpWithConversionEnvelope
                    <ProjectionManagementMessage.Statistics, ProjectionStatisticsHttpFormatted>(
                    _networkSendQueue, http, DefaultFormatter, OkNoCacheResponseConfigurator,
                    status => new ProjectionStatisticsHttpFormatted(status.Projections[0], s => MakeUrl(match, s)),
                    ErrorsEnvelope(http));
            Publish(new ProjectionManagementMessage.GetStatistics(envelope, null, match.BoundVariables["name"], true));
        }

        private void OnProjectionDelete(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;

            var envelope = new SendToHttpEnvelope<ProjectionManagementMessage.Updated>(
                _networkSendQueue, http, DefaultFormatter, OkResponseConfigurator, ErrorsEnvelope(http));
            Publish(
                new ProjectionManagementMessage.Delete(
                    envelope, match.BoundVariables["name"], GetRunAs(http, match),
                    IsOn(match, "deleteCheckpointStream", false),
                    IsOn(match, "deleteStateStream", false)));
        }

        private void OnProjectionStatisticsGet(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;

            var envelope =
                new SendToHttpWithConversionEnvelope
                    <ProjectionManagementMessage.Statistics, ProjectionsStatisticsHttpFormatted>(
                    _networkSendQueue, http, DefaultFormatter, OkNoCacheResponseConfigurator,
                    status => new ProjectionsStatisticsHttpFormatted(status, s => MakeUrl(match, s)),
                    ErrorsEnvelope(http));
            Publish(new ProjectionManagementMessage.GetStatistics(envelope, null, match.BoundVariables["name"], true));
        }

        private void OnProjectionStateGet(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;

            var envelope = new SendToHttpEnvelope<ProjectionManagementMessage.ProjectionState>(
                _networkSendQueue, http, StateFormatter, StateConfigurator, ErrorsEnvelope(http));
            Publish(
                new ProjectionManagementMessage.GetState(
                    envelope, match.BoundVariables["name"], match.BoundVariables["partition"] ?? ""));
        }

        private void OnProjectionResultGet(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;

            var envelope = new SendToHttpEnvelope<ProjectionManagementMessage.ProjectionResult>(
                _networkSendQueue, http, ResultFormatter, ResultConfigurator, ErrorsEnvelope(http));
            Publish(
                new ProjectionManagementMessage.GetResult(
                    envelope, match.BoundVariables["name"], match.BoundVariables["partition"] ?? ""));
        }

        public class ReadEventsBody
        {
            public QuerySourcesDefinition Query { get; set; }
            public JObject Position { get; set; }
            public int? MaxEvents { get; set; }
        }

        private void OnProjectionsReadEvents(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;

            var envelope = new SendToHttpEnvelope<FeedReaderMessage.FeedPage>(
                _networkSendQueue, http, FeedPageFormatter, FeedPageConfigurator, ErrorsEnvelope(http));

            http.ReadTextRequestAsync(
                (o, body) =>
                    {
                        var bodyParsed = body.ParseJson<ReadEventsBody>();
                        var fromPosition = CheckpointTag.FromJson(
                            new JTokenReader(bodyParsed.Position), new ProjectionVersion(0, 0, 0));


                        Publish(
                            new FeedReaderMessage.ReadPage(
                                Guid.NewGuid(),
                                envelope,
                                http.User,
                                bodyParsed.Query,
                                fromPosition.Tag,
                                bodyParsed.MaxEvents ?? 10));
                    },
                x => Log.DebugException(x, "Read Requet Body Failed."));
        }

        private void ProjectionsGet(HttpEntityManager http, UriTemplateMatch match, ProjectionMode? mode)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;

            var envelope =
                new SendToHttpWithConversionEnvelope<ProjectionManagementMessage.Statistics, ProjectionsStatisticsHttpFormatted>(
                    _networkSendQueue, http, DefaultFormatter, OkNoCacheResponseConfigurator,
                    status => new ProjectionsStatisticsHttpFormatted(status, s => MakeUrl(match, s)),
                    ErrorsEnvelope(http));
            Publish(new ProjectionManagementMessage.GetStatistics(envelope, mode, null, true));
        }

        private void ProjectionsPost(HttpEntityManager http, UriTemplateMatch match, ProjectionMode mode, string name)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;

            var envelope = new SendToHttpEnvelope<ProjectionManagementMessage.Updated>(
                _networkSendQueue, http, DefaultFormatter, (codec, message) =>
                    {
                        var localPath = string.Format("/projection/{0}", message.Name);
                        var url = MakeUrl(match, localPath);
                        return new ResponseConfiguration(
                            201, "Created", codec.ContentType, codec.Encoding, new KeyValuePair<string, string>("Location", url));
                    }, ErrorsEnvelope(http));
            http.ReadTextRequestAsync(
                (o, s) =>
                    {
                        ProjectionManagementMessage.Post postMessage;
                        string handlerType = match.BoundVariables["type"] ?? "JS";
                        bool emitEnabled = IsOn(match, "emit", false);
                        bool checkpointsEnabled = mode >= ProjectionMode.Continuous || IsOn(match, "checkpoints", false);
                        bool enabled = IsOn(match, "enabled", def: true);
                        var runAs = GetRunAs(http, match);
                        if (mode <= ProjectionMode.OneTime && string.IsNullOrEmpty(name))
                            postMessage = new ProjectionManagementMessage.Post(
                                envelope, mode, Guid.NewGuid().ToString("D"), runAs, handlerType, s, enabled: enabled,
                                checkpointsEnabled: checkpointsEnabled, emitEnabled: emitEnabled, enableRunAs: true);
                        else
                            postMessage = new ProjectionManagementMessage.Post(
                                envelope, mode, name, runAs, handlerType, s, enabled: enabled,
                                checkpointsEnabled: checkpointsEnabled, emitEnabled: emitEnabled, enableRunAs: true);
                        Publish(postMessage);
                    }, x => Log.DebugException(x, "Reply Text Body Failed."));
        }

        private ResponseConfiguration StateConfigurator(ICodec codec, ProjectionManagementMessage.ProjectionState state)
        {
            if (state.Exception != null)
                return Configure.InternalServerError();
            else
                return state.Position != null
                           ? Configure.Ok("application/json", Helper.UTF8NoBom, null, null, false,
                                          new KeyValuePair<string, string>(SystemHeaders.ProjectionPosition, state.Position.ToJsonString()))
                           : Configure.Ok("application/json", Helper.UTF8NoBom, null, null, false);
        }

        private ResponseConfiguration ResultConfigurator(ICodec codec, ProjectionManagementMessage.ProjectionResult state)
        {
            if (state.Exception != null)
                return Configure.InternalServerError();
            else
                return state.Position != null
                           ? Configure.Ok("application/json", Helper.UTF8NoBom, null, null, false,
                                          new KeyValuePair<string, string>(SystemHeaders.ProjectionPosition, state.Position.ToJsonString()))
                           : Configure.Ok("application/json", Helper.UTF8NoBom, null, null, false);
        }

        private ResponseConfiguration FeedPageConfigurator(ICodec codec, FeedReaderMessage.FeedPage page)
        {
            if (page.Error == FeedReaderMessage.FeedPage.ErrorStatus.NotAuthorized)
                return Configure.Unauthorized();
            return Configure.Ok("application/json", Helper.UTF8NoBom, null, null, false);
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

        private string FeedPageFormatter(ICodec codec, FeedReaderMessage.FeedPage page)
        {
            if (page.Error != FeedReaderMessage.FeedPage.ErrorStatus.Success) 
                return null;

            return new
            {
                CorrelationId = page.CorrelationId,
                ReaderPosition = page.LastReaderPosition.ToJsonRaw(),
                Events = (from e in page.Events
                          let resolvedEvent = e.ResolvedEvent
                          let isJson = resolvedEvent.IsJson
                          let data = isJson 
                                   ? EatException(() => (object) (resolvedEvent.Data.ParseJson<JObject>()), resolvedEvent.Data)
                                   : resolvedEvent.Data
                          let metadata = isJson
                                       ? EatException(() => (object) (resolvedEvent.Metadata.ParseJson<JObject>()), resolvedEvent.Metadata)
                                       : resolvedEvent.Metadata
                          select new
                          {
                              EventStreamId = resolvedEvent.EventStreamId,
                              EventNumber = resolvedEvent.EventSequenceNumber,
                              EventType = resolvedEvent.EventType,
                              Data = data,
                              Metadata = metadata,
                              IsJson = isJson,
                              ReaderPosition = e.ReaderPosition.ToJsonRaw(),
                          }).ToArray()
            }.ToJson();
        }

        private ResponseConfiguration QueryConfigurator(ICodec codec, ProjectionManagementMessage.ProjectionQuery state)
        {
            return Configure.Ok("application/javascript", Helper.UTF8NoBom, null, null, false);
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
            return Configure.Ok("application/json", Helper.UTF8NoBom, null, null, false);
        }

        private ResponseConfiguration OkResponseConfigurator<T>(ICodec codec, T message)
        {
            return new ResponseConfiguration(200, "OK", codec.ContentType, Helper.UTF8NoBom);
        }

        private ResponseConfiguration OkNoCacheResponseConfigurator<T>(ICodec codec, T message)
        {
            return Configure.Ok(codec.ContentType, codec.Encoding, null, null, false);
        }

        private IEnvelope ErrorsEnvelope(HttpEntityManager http)
        {
            return new SendToHttpEnvelope<ProjectionManagementMessage.NotFound>(
                _networkSendQueue,
                http,
                NotFoundFormatter,
                NotFoundConfigurator,
                new SendToHttpEnvelope<ProjectionManagementMessage.NotAuthorized>(
                    _networkSendQueue,
                    http,
                    NotAuthorizedFormatter,
                    NotAuthorizedConfigurator,
                    new SendToHttpEnvelope<ProjectionManagementMessage.Conflict>(
                        _networkSendQueue,
                        http,
                        ConflictFormatter,
                        ConflictConfigurator,
                        new SendToHttpEnvelope<ProjectionManagementMessage.OperationFailed>(
                            _networkSendQueue, http, OperationFailedFormatter, OperationFailedConfigurator, null))));
        }

        private ResponseConfiguration NotFoundConfigurator(ICodec codec, ProjectionManagementMessage.NotFound message)
        {
            return new ResponseConfiguration(404, "Not Found", "text/plain", Helper.UTF8NoBom);
        }

        private string NotFoundFormatter(ICodec codec, ProjectionManagementMessage.NotFound message)
        {
            return message.Reason;
        }

        private ResponseConfiguration NotAuthorizedConfigurator(
            ICodec codec, ProjectionManagementMessage.NotAuthorized message)
        {
            return new ResponseConfiguration(401, "Not Authorized", "text/plain", Encoding.UTF8);
        }

        private string NotAuthorizedFormatter(ICodec codec, ProjectionManagementMessage.NotAuthorized message)
        {
            return message.Reason;
        }

        private ResponseConfiguration OperationFailedConfigurator(
            ICodec codec, ProjectionManagementMessage.OperationFailed message)
        {
            return new ResponseConfiguration(500, "Failed", "text/plain", Helper.UTF8NoBom);
        }

        private string OperationFailedFormatter(ICodec codec, ProjectionManagementMessage.OperationFailed message)
        {
            return message.Reason;
        }
        
        private ResponseConfiguration ConflictConfigurator(
            ICodec codec, ProjectionManagementMessage.OperationFailed message)
        {
            return new ResponseConfiguration(409, "Conflict", "text/plain", Helper.UTF8NoBom);
        }

        private string ConflictFormatter(ICodec codec, ProjectionManagementMessage.OperationFailed message)
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

        private static ProjectionManagementMessage.RunAs GetRunAs(HttpEntityManager http, UriTemplateMatch match)
        {
            return new ProjectionManagementMessage.RunAs(http.User);
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

        public static T EatException<T>(Func<T> func, T defaultValue = default(T))
        {
            Ensure.NotNull(func, "func");
            try
            {
                return func();
            }
            catch (Exception)
            {
                return defaultValue;
            }
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
