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
using System.Collections.Specialized;
using System.Diagnostics;
using System.Net;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.Transport.Http.Codecs;
using EventStore.Transport.Http;
using System.Linq;
using EventStore.Transport.Http.EntityManagement;
using EventStore.Transport.Http.Server;
using HttpStatusCode = EventStore.Transport.Http.HttpStatusCode;
using Uri = System.Uri;

namespace EventStore.Core.Services.Transport.Http
{
    public class UriToActionMatch
    {
        public readonly UriTemplateMatch TemplateMatch;
        public readonly ControllerAction ControllerAction;
        public readonly Action<HttpEntity, UriTemplateMatch> RequestHandler;

        public UriToActionMatch(UriTemplateMatch templateMatch, 
                                ControllerAction controllerAction, 
                                Action<HttpEntity, UriTemplateMatch> requestHandler)
        {
            TemplateMatch = templateMatch;
            ControllerAction = controllerAction;
            RequestHandler = requestHandler;
        }
    }

    public enum ServiceAccessibility
    {
        Private,
        Public
    }

    public interface IHttpService
    {
        void RegisterControllerAction(ControllerAction action, Action<HttpEntity, UriTemplateMatch> handler);
    }

    public class HttpService : IHttpService,
                               IHandle<SystemMessage.SystemInit>,
                               IHandle<SystemMessage.BecomeShuttingDown>,
                               IHandle<HttpMessage.SendOverHttp>,
                               IHandle<HttpMessage.UpdatePendingRequests>
    {
        private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan MaxDuration = TimeSpan.FromSeconds(5);
        private static readonly string[] Empty = new string[0];

        private static readonly ILogger Log = LogManager.GetLoggerFor<HttpService>();

        public bool IsListening { get { return _server.IsListening; } }

        private readonly ServiceAccessibility _accessibility;
        private readonly IPublisher _inputBus;
        private readonly IEnvelope _publishEnvelope;

        private readonly Common.Concurrent.ConcurrentQueue<HttpEntity> _pending = new Common.Concurrent.ConcurrentQueue<HttpEntity>();
        private readonly List<HttpRoute> _actions;

        private readonly HttpMessagePipe _httpPipe;
        private readonly HttpAsyncServer _server;
        private readonly QueuedHandler[] _receiveHandlers;
        private long _requestNumber = -1;

        public HttpService(ServiceAccessibility accessibility, IPublisher inputBus, string[] prefixes, int receiveHandlerCount)
        {
            Ensure.NotNull(inputBus, "inputBus");
            Ensure.NotNull(prefixes, "prefixes");
            Ensure.Positive(receiveHandlerCount, "receiveHandlerCount");
            _accessibility = accessibility;
            _inputBus = inputBus;
            _publishEnvelope = new PublishEnvelope(inputBus);

            _actions = new List<HttpRoute>();

            _httpPipe = new HttpMessagePipe();
            _receiveHandlers = new QueuedHandler[receiveHandlerCount];
            for (int i = 0; i < receiveHandlerCount; i++)
            {
                var handler = new QueuedHandler(new NarrowingHandler<Message, IncomingHttpRequestMessage>(new ProcessHttpHandler(this)),
                                                "Incoming HTTP #" + (i + 1),
                                                true,
                                                50);
                _receiveHandlers[i] = handler;
                handler.Start();
            }
            _server = new HttpAsyncServer(prefixes);
            _server.RequestReceived += RequestReceived;
        }

        public void Handle(SystemMessage.SystemInit message)
        {
            if (_server.TryStart())
            {
                _inputBus.Publish(TimerMessage.Schedule.Create(UpdateInterval,
                                                               _publishEnvelope,
                                                               new HttpMessage.UpdatePendingRequests(_accessibility)));
            }
            else
                Application.Exit(ExitCode.Error, "Http async server failed to start.");
        }

        public void Handle(SystemMessage.BecomeShuttingDown message)
        {

        }

        public void Handle(HttpMessage.SendOverHttp message)
        {
            _httpPipe.Push(message.Message, message.EndPoint);
        }

        public void Handle(HttpMessage.UpdatePendingRequests message)
        {
            if (_accessibility != message.Accessibility)
                return;

            PurgeTimedOutRequests();

            _inputBus.Publish(TimerMessage.Schedule.Create(UpdateInterval,
                                                           _publishEnvelope,
                                                           new HttpMessage.UpdatePendingRequests(_accessibility)));
        }

        private void PurgeTimedOutRequests()
        {
#if DO_NOT_TIMEOUT_REQUESTS 
            return;
#endif
            // pending request are almost perfectly sorted by DateTime.UtcNow, no need to use SortedSet
            HttpEntity request;
            while (_pending.TryPeek(out request) && DateTime.UtcNow - request.TimeStamp > MaxDuration)
            {
                request.Manager.ReplyStatus(
                    HttpStatusCode.RequestTimeout,
                    "Server was unable to handle request in time",
                    e => Log.ErrorException(e, "Error occurred while closing timed out connection (http service core)."));

                HttpEntity req;
                if (!_pending.TryDequeue(out req) || !ReferenceEquals(request, req))
                    throw new Exception("Concurrent removing from pending requests queue.");
            }
        }

        public void Shutdown()
        {
            _server.Shutdown();
        }

        public void SetupController(IController controller)
        {
            Ensure.NotNull(controller, "controller");
            controller.Subscribe(this, _httpPipe);
        }

        public void RegisterControllerAction(ControllerAction action, Action<HttpEntity, UriTemplateMatch> handler)
        {
            Ensure.NotNull(action, "action");
            Ensure.NotNull(handler, "handler");

            Debug.Assert(!_actions.Contains(x => x.Action.Equals(action)), "Duplicate controller actions.");
            _actions.Add(new HttpRoute(action, handler));
        }


        private void RequestReceived(HttpAsyncServer sender, HttpListenerContext context)
        {
            var queueNum = (int)(Interlocked.Increment(ref _requestNumber) % _receiveHandlers.Length);
            _receiveHandlers[queueNum].Handle(new IncomingHttpRequestMessage(sender, context));
        }

        private void ProcessRequest(HttpAsyncServer sender, HttpListenerContext context)
        {
/*
            try
            {
                PurgeTimedOutRequests();
            }
            catch (Exception exc)
            {
                Log.ErrorException(exc, "Error during purging timed-out request.");
            }
*/

            try
            {
                var allMatches = GetAllUriMatches(context.Request.Url);
                if (allMatches.Count == 0)
                {
                    NotFound(context);
                    return;
                }

                var allowedMethods = new string[allMatches.Count + 1];
                for (int i = 0; i < allMatches.Count; ++i)
                {
                    allowedMethods[i] = allMatches[i].ControllerAction.HttpMethod;
                }
                //add options to the list of allowed request methods
                allowedMethods[allMatches.Count] = HttpMethod.Options;

                if (context.Request.HttpMethod.Equals(HttpMethod.Options, StringComparison.OrdinalIgnoreCase))
                {
                    RespondWithOptions(context, allowedMethods);
                    return;
                }

                var match = allMatches.LastOrDefault(m => 
                    m.ControllerAction.HttpMethod.Equals(context.Request.HttpMethod, StringComparison.OrdinalIgnoreCase));
                if (match == null)
                {
                    MethodNotAllowed(context, allowedMethods);
                    return;
                }

                ICodec requestCodec = SelectRequestCodec(context.Request.HttpMethod,
                                                         context.Request.ContentType,
                                                         match.ControllerAction.SupportedRequestCodecs);
                if (requestCodec == null)
                {
                    BadCodec(context, "Content-Type MUST be set for POST, PUT and DELETE.");
                    return;
                }

                ICodec responseCodec = SelectResponseCodec(context.Request.QueryString,
                                                           context.Request.AcceptTypes,
                                                           match.ControllerAction.SupportedResponseCodecs,
                                                           match.ControllerAction.DefaultResponseCodec);
                if (responseCodec == null)
                {
                    BadCodec(context, "Requested uri is not available in requested format");
                    return;
                }

                var entity = CreateEntity(DateTime.UtcNow,
                                          context,
                                          requestCodec,
                                          responseCodec,
                                          allowedMethods,
                                          satisfied => { });
                _pending.Enqueue(entity);
                match.RequestHandler(entity, match.TemplateMatch);
            }
            catch (Exception exception)
            {
                Log.ErrorException(exception, "Unhandled exception while processing http request.");
                InternalServerError(context);
            }
        }

        private List<UriToActionMatch> GetAllUriMatches(Uri uri)
        {
            var matches = new List<UriToActionMatch>();
            for (int i = 0; i < _actions.Count; ++i)
            {
                var route = _actions[i];
                var match = route.UriTemplate.Match(new UriBuilder(uri.Scheme, uri.Host, uri.Port).Uri, uri);
                if (match != null)
                    matches.Add(new UriToActionMatch(match, route.Action, route.Handler));
            }
            return matches;
        }

        private void RespondWithOptions(HttpListenerContext context, string[] allowed)
        {
            var entity = CreateEntity(DateTime.UtcNow, context, Codec.NoCodec, Codec.NoCodec, allowed, _ => { });
            entity.Manager.ReplyStatus(HttpStatusCode.OK,
                                       "OK",
                                       e => Log.ErrorException(e, "Error while closing http connection (http service core)."));
        }

        private void MethodNotAllowed(HttpListenerContext context, string[] allowed)
        {
            var entity = CreateEntity(DateTime.UtcNow, context, Codec.NoCodec, Codec.NoCodec, allowed, _ => { });
            entity.Manager.ReplyStatus(HttpStatusCode.MethodNotAllowed,
                                       "Method Not Allowed",
                                       e => Log.ErrorException(e, "Error while closing http connection (http service core)."));
        }

        private void NotFound(HttpListenerContext context)
        {
            var entity = CreateEntity(DateTime.UtcNow, context, Codec.NoCodec, Codec.NoCodec, Empty, _ => { });
            entity.Manager.ReplyStatus(HttpStatusCode.NotFound,
                                       "Not Found",
                                       e => Log.ErrorException(e, "Error while closing http connection (http service core)."));
        }

        private void InternalServerError(HttpListenerContext context)
        {
            var entity = CreateEntity(DateTime.UtcNow, context, Codec.NoCodec, Codec.NoCodec, Empty, _ => { });
            entity.Manager.ReplyStatus(HttpStatusCode.InternalServerError,
                                       "Internal Server Error",
                                       e => Log.ErrorException(e, "Error while closing http connection (http service core)."));
        }

        private void BadCodec(HttpListenerContext context, string reason)
        {
            var entity = CreateEntity(DateTime.UtcNow, context, Codec.NoCodec, Codec.NoCodec, Empty, _ => { });
            entity.Manager.ReplyStatus(HttpStatusCode.UnsupportedMediaType,
                                       reason,
                                       e => Log.ErrorException(e, "Error while closing http connection (http service core)."));
        }

        private HttpEntity CreateEntity(DateTime receivedTime,
                                        HttpListenerContext context,
                                        ICodec requestCodec,
                                        ICodec responseCodec,
                                        string[] allowedMethods,
                                        Action<HttpEntity> onRequestSatisfied)
        {
            return new HttpEntity(receivedTime, requestCodec, responseCodec, context, allowedMethods, onRequestSatisfied);
        }

        private ICodec SelectRequestCodec(string method, string contentType, IEnumerable<ICodec> supportedCodecs)
        {
            switch (method.ToUpper())
            {
                case HttpMethod.Post:
                case HttpMethod.Put:
                case HttpMethod.Delete:
                    return supportedCodecs.SingleOrDefault(c => c.CanParse(contentType));

                default:
                    return Codec.NoCodec;
            }
        }

        private ICodec SelectResponseCodec(NameValueCollection query, string[] acceptTypes, ICodec[] supported, ICodec @default)
        {
            var requestedFormat = GetFormatOrDefault(query);
            if (requestedFormat == null && acceptTypes.IsEmpty())
                return @default;

            if (requestedFormat != null)
                return supported.FirstOrDefault(c => c.SuitableForReponse(AcceptComponent.Parse(requestedFormat)));

            return acceptTypes.Select(AcceptComponent.TryParse)
                              .Where(x => x != null)
                              .OrderByDescending(v => v.Priority)
                              .Select(type => supported.FirstOrDefault(codec => codec.SuitableForReponse(type)))
                              .FirstOrDefault(corresponding => corresponding != null);
        }

        private string GetFormatOrDefault(NameValueCollection query)
        {
            var format = (query != null && query.Count > 0) ? query.Get("format") : null;
            if (format == null)
                return null;
            switch (format.ToLower())
            {
                case "json":
                    return ContentType.Json;
                case "text":
                    return ContentType.PlainText;
                case "xml":
                    return ContentType.Xml;
                case "atom":
                    return ContentType.Atom;
                case "atomxj":
                    return ContentType.AtomJson;
                case "atomsvc":
                    return ContentType.AtomServiceDoc;
                case "atomsvcxj":
                    return ContentType.AtomServiceDocJson;
                default:
                    throw new NotSupportedException("Unknown format requested");
            }
        }

        private class ProcessHttpHandler : IHandle<IncomingHttpRequestMessage>
        {
            private readonly HttpService _parent;

            public ProcessHttpHandler(HttpService parent)
            {
                _parent = parent;
            }

            public void Handle(IncomingHttpRequestMessage message)
            {
                _parent.ProcessRequest(message.Sender, message.Context);
            }
        }

        private class IncomingHttpRequestMessage : Message
        {
            public readonly HttpAsyncServer Sender;
            public readonly HttpListenerContext Context;

            public IncomingHttpRequestMessage(HttpAsyncServer sender, HttpListenerContext context)
            {
                Sender = sender;
                Context = context;
            }
        }

        private struct HttpRoute
        {
            public readonly ControllerAction Action;
            public readonly Action<HttpEntity, UriTemplateMatch> Handler;
            public readonly UriTemplate UriTemplate;

            public HttpRoute(ControllerAction action, Action<HttpEntity, UriTemplateMatch> handler)
            {
                Action = action;
                Handler = handler;
                UriTemplate = new UriTemplate(action.UriTemplate);
            }
        }
    }
}