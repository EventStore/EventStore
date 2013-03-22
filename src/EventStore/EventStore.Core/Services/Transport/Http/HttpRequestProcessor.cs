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
using System.Linq;
using System.Net;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Http.Codecs;
using EventStore.Transport.Http;
using EventStore.Transport.Http.EntityManagement;
using EventStore.Transport.Http.Server;
using HttpStatusCode = EventStore.Transport.Http.HttpStatusCode;

namespace EventStore.Core.Services.Transport.Http
{
    internal class IncomingHttpRequestMessage : Message
    {
        public readonly HttpAsyncServer Sender;
        public readonly HttpListenerContext Context;

        public IncomingHttpRequestMessage(HttpAsyncServer sender, HttpListenerContext context)
        {
            Sender = sender;
            Context = context;
        }
    }

    internal class HttpRequestProcessor : IHandle<IncomingHttpRequestMessage>, 
                                          IHandle<HttpMessage.PurgeTimedOutRequests>
    {
        private static readonly TimeSpan MaxRequestDuration = TimeSpan.FromSeconds(10);
        private static readonly ILogger Log = LogManager.GetLoggerFor<HttpRequestProcessor>();
        
        private readonly HttpService _httpService;
        private readonly Queue<HttpEntity> _pending = new Queue<HttpEntity>();

        public HttpRequestProcessor(HttpService httpService)
        {
            Ensure.NotNull(httpService, "httpService");
            _httpService = httpService;
        }

        public void Handle(HttpMessage.PurgeTimedOutRequests message)
        {
            PurgeTimedOutRequests();
        }

        private void PurgeTimedOutRequests()
        {
            try 
            {
                var now = DateTime.UtcNow;
                // pending request are almost perfectly sorted by DateTime.UtcNow, no need to use SortedSet
                while (_pending.Count > 0 && now - _pending.Peek().TimeStamp > MaxRequestDuration)
                {
                    var request = _pending.Dequeue();
                    
                    if (Application.IsDefined("DO_NOT_TIMEOUT_REQUESTS"))
                        continue;

                    if (!request.Manager.IsProcessing)
                    {
                        request.Manager.ReplyStatus(
                            HttpStatusCode.RequestTimeout,
                            "Server was unable to handle request in time",
                            e => Log.ErrorException(e, "Error occurred while closing timed out connection (http service core)."));
                    }
                }
            }
            catch (Exception exc)
            {
                Log.ErrorException(exc,
                                   "Error purging timed out requests in HTTP request processor at [{0}].",
                                   string.Join(", ", _httpService.ListenPrefixes));
            }
        }

        public void Handle(IncomingHttpRequestMessage message)
        {
            ProcessRequest(message.Context);
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            try
            {
                //TODO: probably we should pass HttpVerb into matches
                var allMatches = _httpService.GetAllUriMatches(context.Request.Url);
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
                    BadCodec(context, "Requested URI is not available in requested format");
                    return;
                }

                var entity = CreateEntity(DateTime.UtcNow, context, requestCodec, responseCodec, allowedMethods, satisfied => { });
                _pending.Enqueue(entity);
                match.RequestHandler(entity, match.TemplateMatch);
            }
            catch (Exception exception)
            {
                Log.ErrorException(exception,
                                   "Unhandled exception while processing http request at [{0}].",
                                   string.Join(", ", _httpService.ListenPrefixes));
                InternalServerError(context);
            }

            PurgeTimedOutRequests();
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
            var entity = CreateEntity(DateTime.UtcNow, context, Codec.NoCodec, Codec.NoCodec, Common.Utils.Empty.StringArray, _ => { });
            entity.Manager.ReplyStatus(HttpStatusCode.NotFound,
                                       "Not Found",
                                       e => Log.ErrorException(e, "Error while closing http connection (http service core)."));
        }

        private void InternalServerError(HttpListenerContext context)
        {
            var entity = CreateEntity(DateTime.UtcNow, context, Codec.NoCodec, Codec.NoCodec, Common.Utils.Empty.StringArray, _ => { });
            entity.Manager.ReplyStatus(HttpStatusCode.InternalServerError,
                                       "Internal Server Error",
                                       e => Log.ErrorException(e, "Error while closing http connection (http service core)."));
        }

        private void BadCodec(HttpListenerContext context, string reason)
        {
            var entity = CreateEntity(DateTime.UtcNow, context, Codec.NoCodec, Codec.NoCodec, Common.Utils.Empty.StringArray, _ => { });
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
                    return supportedCodecs.SingleOrDefault(c => c.CanParse(MediaType.Parse(contentType)));

                default:
                    return Codec.NoCodec;
            }
        }

        private ICodec SelectResponseCodec(
            NameValueCollection query, string[] acceptTypes, ICodec[] supported, ICodec @default)
        {
            var requestedFormat = GetFormatOrDefault(query);
            if (requestedFormat == null && acceptTypes.IsEmpty())
                return @default;

            if (requestedFormat != null)
                return supported.FirstOrDefault(c => c.SuitableForResponse(MediaType.Parse(requestedFormat)));

            return acceptTypes.Select(MediaType.TryParse)
                              .Where(x => x != null)
                              .OrderByDescending(v => v.Priority)
                              .Select(type => supported.FirstOrDefault(codec => codec.SuitableForResponse(type)))
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
    }
}