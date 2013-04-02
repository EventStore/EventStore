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
using System.Globalization;
using System.Text;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Http.Codecs;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Atom;
using EventStore.Transport.Http.EntityManagement;
using Newtonsoft.Json;
using EventStore.Common.Utils;

namespace EventStore.Core.Services.Transport.Http.Controllers
{
    public enum EmbedLevel
    {
        None,
        Rich,
        Body,
        PrettyBody,
        TryHarder
    }

    public class AtomController : CommunicationController
    {
        private static readonly HtmlFeedCodec HtmlFeedCodec = new HtmlFeedCodec(); // initialization order matters
        private static readonly ICodec EventStoreJsonCodec = Codec.CreateCustom(Codec.Json, ContentType.AtomJson, Encoding.UTF8);

        private static readonly ICodec[] AtomCodecs = new[]
                                                      {
                                                          EventStoreJsonCodec,
                                                          Codec.Xml,
                                                          Codec.ApplicationXml,
                                                          Codec.CreateCustom(Codec.Xml, ContentType.Atom, Encoding.UTF8),
                                                          Codec.Json,
                                                          
                                                      };
        private static readonly ICodec[] AtomWithHtmlCodecs = new[]
                                                              {
                                                                  EventStoreJsonCodec,
                                                                  Codec.Xml,
                                                                  Codec.ApplicationXml,
                                                                  Codec.CreateCustom(Codec.Xml, ContentType.Atom, Encoding.UTF8),
                                                                  Codec.Json,
                                                                  HtmlFeedCodec // initialization order matters
                                                              };


        private readonly GenericController _genericController;
        private readonly AllEventsController _allEventsController;
        private readonly IPublisher _networkSendQueue;

        public AtomController(IPublisher publisher, IPublisher networkSendQueue)
            : base(publisher)
        {
            _networkSendQueue = networkSendQueue;
            _genericController = new GenericController(publisher, networkSendQueue);
            _allEventsController = new AllEventsController(publisher, networkSendQueue);
        }

        protected override void SubscribeCore(IHttpService http, HttpMessagePipe pipe)
        {
            Register(http, "/streams", HttpMethod.Post, OnCreateStream, AtomCodecs, AtomCodecs);
            Register(http, "/streams/{stream}", HttpMethod.Delete, OnDeleteStream, AtomCodecs, AtomCodecs);
            Register(
                http, "/streams/{stream}?embed={embed}", HttpMethod.Get, OnGetStreamFeedLatest, Codec.NoCodecs,
                AtomWithHtmlCodecs);
            Register(
                http, "/streams/{stream}/range/{start}/{count}?embed={embed}", HttpMethod.Get, OnGetStreamRangeFeedPage,
                Codec.NoCodecs, AtomWithHtmlCodecs);
            Register(http, "/streams/{stream}/{id}", HttpMethod.Get, OnGetEntry, Codec.NoCodecs, AtomWithHtmlCodecs);
            Register(http, "/streams/{stream}", HttpMethod.Post, OnPostEntry, AtomCodecs, AtomCodecs);
            Register(
                http, "/streams/$all?embed={embed}", HttpMethod.Get, OnGetAllFeedBeforeHead, Codec.NoCodecs,
                AtomWithHtmlCodecs);
            Register(
                http, "/streams/$all/{count}?embed={embed}", HttpMethod.Get, OnGetAllFeedBeforeHead, Codec.NoCodecs,
                AtomWithHtmlCodecs);
            Register(
                http, "/streams/$all/before/{pos}/{count}?embed={embed}", HttpMethod.Get, OnGetAllFeedBefore,
                Codec.NoCodecs, AtomWithHtmlCodecs);
            Register(
                http, "/streams/$all/after/{pos}/{count}?embed={embed}", HttpMethod.Get, OnGetAllAfterFeed,
                Codec.NoCodecs, AtomWithHtmlCodecs);
        }

        //FEED

        private void OnCreateStream(HttpEntityManager entity, UriTemplateMatch match)
        {
            _genericController.CreateStream(entity);
        }

        private void OnDeleteStream(HttpEntityManager entity, UriTemplateMatch match)
        {
            var stream = match.BoundVariables["stream"];
            if (string.IsNullOrEmpty(stream))
            {
                SendBadRequest(entity, string.Format("Invalid stream name '{0}'", stream));
                return;
            }

            _genericController.DeleteStream(entity, stream);
        }

        private void OnGetStreamFeedLatest(HttpEntityManager entity, UriTemplateMatch match)
        {
            var stream = match.BoundVariables["stream"];
            var embed = GetEmbed(entity, match);
            if (string.IsNullOrEmpty(stream))
            {
                SendBadRequest(entity, string.Format("Invalid stream name '{0}'", stream));
                return;
            }

            OnGetStreamFeedCore(entity, stream, -1, AtomSpecs.FeedPageSize, embed, headOfStream: true);
        }

        private void OnGetStreamRangeFeedPage(HttpEntityManager entity, UriTemplateMatch match)
        {
            var stream = match.BoundVariables["stream"];
            var start = match.BoundVariables["start"];
            var count = match.BoundVariables["count"];
            var embed = GetEmbed(entity, match);
            
            int startIdx;
            int cnt;

            if (string.IsNullOrEmpty(stream))
            {
                SendBadRequest(entity, string.Format("Invalid stream name '{0}'", stream));
                return;
            }
            if (!int.TryParse(start, out startIdx) || startIdx < -1)
            {
                SendBadRequest(entity, string.Format("'{0}' is not valid start index", start));
                return;
            }
            if (!int.TryParse(count, out cnt) || cnt <= 0)
            {
                SendBadRequest(entity, string.Format("'{0}' is not valid count. Should be positive integer", count));
                return;
            }
            OnGetStreamFeedCore(entity, stream, startIdx, cnt, embed, headOfStream: startIdx == -1);
        }

        private void OnGetStreamFeedCore(HttpEntityManager entity, string stream, int start, int count, EmbedLevel embed, bool headOfStream)
        {
            var etag = entity.HttpEntity.Request.Headers["If-None-Match"];
            int? validationStreamVersion = null;
            //TODO: extract 
            // etag format is version;contenttypehash
            if (etag != null)
            {
                var trimmed = etag.Trim('\"');
                var splitted = trimmed.Split(new[] {';'});
                if (splitted.Length == 2)
                {
                    var typeHash = entity.ResponseCodec.ContentType.GetHashCode();
                    int streamVersion;
                    validationStreamVersion = splitted[1] == typeHash.ToString(CultureInfo.InvariantCulture)
                                              && etag.IsNotEmptyString() && int.TryParse(splitted[0], out streamVersion)
                                                  ? (int?) streamVersion
                                                  : null;
                }
            }
            _genericController.GetStreamFeedPage(
                entity, stream, start, count, embed, validationStreamVersion, headOfStream);
        }

        private static EmbedLevel GetEmbed(HttpEntityManager entity, UriTemplateMatch match, EmbedLevel htmlLevel = EmbedLevel.PrettyBody)
        {
            if (entity.ResponseCodec is IRichAtomCodec)
                return htmlLevel;
            var rawValue = match.BoundVariables["embed"];
            switch ((rawValue ?? "").ToLowerInvariant())
            {
                case "rich": return EmbedLevel.Rich;
                case "body": return EmbedLevel.Body;
                case "pretty": return EmbedLevel.PrettyBody;
                case "tryharder": return EmbedLevel.TryHarder;
                default: return EmbedLevel.None;
            }
        }

        //$ALL

        private void OnGetAllFeedBeforeHead(HttpEntityManager entity, UriTemplateMatch match)
        {
            var c = match.BoundVariables["count"];
            var embed = GetEmbed(entity, match);

            int count;
            if (!string.IsNullOrEmpty(c))
            {
                if (!int.TryParse(c, out count))
                    SendBadRequest(entity, string.Format("Invalid count argument : {0}", c));
            }
            else
            {
                count = AtomSpecs.FeedPageSize;
            }

            _allEventsController.GetAllBeforeFeed(entity, TFPos.Invalid, count, embed, headOfTf: true);
        }

        private void OnGetAllFeedBefore(HttpEntityManager entity, UriTemplateMatch match)
        {
            var p = match.BoundVariables["pos"];
            var c = match.BoundVariables["count"];
            var embed = GetEmbed(entity, match);

            TFPos position;
            int count;

            if (!string.IsNullOrEmpty(p))
            {
                if (!TFPos.TryParse(p, out position))
                    SendBadRequest(entity, string.Format("Invalid position argument : {0}", p));
            }
            else
            {
                position = TFPos.Invalid;
            }

            if (!string.IsNullOrEmpty(c))
            {
                if (!int.TryParse(c, out count))
                    SendBadRequest(entity, string.Format("Invalid count argument : {0}", c));
            }
            else
            {
                count = AtomSpecs.FeedPageSize;
            }

            _allEventsController.GetAllBeforeFeed(entity, position, count, embed, headOfTf: false);
        }

        private void OnGetAllAfterFeed(HttpEntityManager entity, UriTemplateMatch match)
        {
            var p = match.BoundVariables["pos"];
            var c = match.BoundVariables["count"];
            var embed = GetEmbed(entity, match);

            TFPos position;
            int count;

            if (string.IsNullOrEmpty(p) || !TFPos.TryParse(p, out position))
            {
                SendBadRequest(entity, string.Format("Invalid position argument : {0}", p));
                return;
            }
            if (string.IsNullOrEmpty(c) || !int.TryParse(c, out count))
            {
                SendBadRequest(entity, string.Format("Invalid count argument : {0}", c));
                return;
            }

            _allEventsController.GetAllAfterFeed(entity, position, count, embed, headOfTf: false);
        }

        //ENTRY MANIPULATION

        private void OnGetEntry(HttpEntityManager entity, UriTemplateMatch match)
        {
            var stream = match.BoundVariables["stream"];
            var id = match.BoundVariables["id"];
            var embed = GetEmbed(entity, match, htmlLevel: EmbedLevel.TryHarder);
            int version;
            if (string.IsNullOrEmpty(stream) || !int.TryParse(id, out version))
            {
                SendBadRequest(entity, "Stream must bu non-empty string and id must be integer value");
                return;
            }

            _genericController.GetEntry(entity, stream, version, embed);
        }

        private void OnPostEntry(HttpEntityManager entity, UriTemplateMatch match)
        {
            var stream = match.BoundVariables["stream"];
            if (string.IsNullOrEmpty(stream))
            {
                SendBadRequest(entity, string.Format("Invalid request. Stream must be non-empty string"));
                return;
            }

            _genericController.PostEntry(entity, stream);
        }
    }

    class HtmlFeedCodec : ICodec, IRichAtomCodec
    {
        public string ContentType  { get { return "text/html"; } }
        public Encoding Encoding { get { return Encoding.UTF8; } }

        public bool CanParse(MediaType format)
        {
            throw new NotImplementedException();
        }

        public bool SuitableForResponse(MediaType component)
        {
            return component.Type == "*"
                   || (string.Equals(component.Type, "text", StringComparison.OrdinalIgnoreCase)
                       && (component.Subtype == "*" || string.Equals(component.Subtype, "html", StringComparison.OrdinalIgnoreCase)));
        }

        public T From<T>(string text)
        {
            throw new NotImplementedException();
        }

        public string To<T>(T value)
        {
            return @"
<!DOCTYPE html>
<html>
<head>
    <script src='/web/es/lib/jquery/jquery-1.8.0.min.js'></script>
    <script src='/web/es/lib/jquery/jquery-ui-1.8.23.min.js'></script>
    <script src='/web/es/lib/jsrender/jsrender.js'></script>
    <script src='/web/es/js/atom/render.js'></script>
    <script src='/web/es/js/es.tmpl.js'></script>
    <script id='r-head'>
        es.tmpl.renderHead();
    </script>
</head>
<body>
<script>
    var data = " + JsonConvert.SerializeObject(value, Formatting.Indented, JsonCodec.JsonSettings) + @";
    var templateJs = '/web/es/js/atom/" + value.GetType().Name + @".html';

    function reRenderData(data) {
        renderHtmlBy(data, templateJs);
    }

    $(function() {
        reRenderData(data);
    }); 
</script>

<div id='content'>
    <div id='data'></div>
    <script id='r-body'>
    es.tmpl.renderBody();
    </script>
</div>

</body>
</html>
";
        }
    }

    interface IRichAtomCodec
    {
    }

    public class GenericController : CommunicationController
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<GenericController>();
        
        private readonly IPublisher _networkSendQueue;

        public GenericController(IPublisher publisher, IPublisher networkSendQueue)
            : base(publisher)
        {
            _networkSendQueue = networkSendQueue;
        }

        protected override void SubscribeCore(IHttpService service, HttpMessagePipe pipe)
        {
            //no direct subscriptions
        }

        public void CreateStream(HttpEntityManager entity)
        {
            entity.ReadTextRequestAsync(CreateStreamBodyRead,
                                                e => Log.ErrorException(e, "Error while reading request (CREATE stream)."));
        }

        private void CreateStreamBodyRead(HttpEntityManager manager, string body)
        {
            var create = manager.RequestCodec.From<HttpClientMessageDto.CreateStreamText>(body);
            if (create == null)
            {
                SendBadRequest(manager, "Create stream request body cannot be deserialized");
                return;
            }

            var eventStreamId = create.EventStreamId;
            var envelope = new SendToHttpEnvelope(_networkSendQueue,
                                                  manager,
                                                  Format.Atom.CreateStreamCompleted,
                                                  (a, m) => Configure.CreateStreamCompleted(a, m, eventStreamId));
            var msg = new ClientMessage.CreateStream(Guid.NewGuid(),
                                                     envelope,
                                                     true, 
                                                     create.EventStreamId,
                                                     Guid.NewGuid(), 
                                                     false,//TODO TR discover
                                                     Encoding.UTF8.GetBytes(create.Metadata ?? string.Empty));
            Publish(msg);
        }

        public void DeleteStream(HttpEntityManager entity, string stream)
        {
            entity.AsyncState = stream;
            entity.ReadTextRequestAsync(DeleteStreamBodyRead,
                                                e => Log.ErrorException(e, "Error while reading request (DELETE stream)."));
        }

        private void DeleteStreamBodyRead(HttpEntityManager manager, string body)
        {
            var stream = (string)manager.AsyncState;

            var delete = manager.RequestCodec.From<HttpClientMessageDto.DeleteStreamText>(body);
            if (delete == null)
            {
                SendBadRequest(manager, "Delete stream request body cannot be deserialized");
                return;
            }

            var envelope = new SendToHttpEnvelope(_networkSendQueue,
                                                  manager,
                                                  Format.Atom.DeleteStreamCompleted,
                                                  Configure.DeleteStreamCompleted);
            var msg = new ClientMessage.DeleteStream(Guid.NewGuid(), envelope, true, stream, delete.ExpectedVersion);
            Publish(msg);
        }

        public void GetStreamFeedPage(HttpEntityManager entity, 
                                      string stream, 
                                      int start, 
                                      int count, 
                                      EmbedLevel embed, 
                                      int? validationStreamVersion,
                                      bool headOfStream)
        {
            entity.AsyncState = start;
            var envelope = new SendToHttpEnvelope(_networkSendQueue,
                                                  entity,
                                                  (ent, msg) => Format.Atom.ReadStreamEventsBackwardCompletedFeed(ent, msg, embed, headOfStream),
                                                  (args, msg) => Configure.ReadStreamEventsBackwardCompleted(args, msg, headOfStream));
            Publish(new ClientMessage.ReadStreamEventsBackward(Guid.NewGuid(),
                                                               envelope,
                                                               stream,
                                                               start,
                                                               count,
                                                               resolveLinks: true,
                                                               validationStreamVersion: validationStreamVersion));
        }

        public void GetEntry(HttpEntityManager entity, string stream, int version, EmbedLevel embed)
        {
            var envelope = new SendToHttpEnvelope(_networkSendQueue, entity,
                                                  (args, message) => Format.Atom.ReadEventCompletedEntry(args, message, embed), 
                                                  Configure.ReadEventCompleted);
            Publish(new ClientMessage.ReadEvent(Guid.NewGuid(), envelope, stream, version, true));
        }

        public void PostEntry(HttpEntityManager entity, string stream)
        {
            entity.AsyncState = stream;
            entity.ReadTextRequestAsync(OnPostEntryRequestRead, 
                                                e => Log.ErrorException(e, "Error while reading request (POST entry)."));
        }

        private void OnPostEntryRequestRead(HttpEntityManager manager, string body)
        {
            var eventStreamId = (string)manager.AsyncState;

            var parsed = AutoEventConverter.SmartParse(body, manager.RequestCodec);
            var expectedVersion = parsed.Item1;
            var events = parsed.Item2;

            if (events.IsEmpty())
            {
                SendBadRequest(manager, "Write request body invalid");
                return;
            }

            var envelope = new SendToHttpEnvelope(_networkSendQueue,
                                                  manager,
                                                  Format.WriteEventsCompleted,
                                                  (a, m) => Configure.WriteEventsCompleted(a, m, eventStreamId));
            var msg = new ClientMessage.WriteEvents(Guid.NewGuid(), envelope, true, eventStreamId, expectedVersion, events);

            Publish(msg);
        }
    }

    public class AllEventsController : CommunicationController
    {
        private readonly IPublisher _networkSendQueue;

        public AllEventsController(IPublisher publisher, IPublisher networkSendQueue)
            : base(publisher)
        {
            _networkSendQueue = networkSendQueue;
        }

        protected override void SubscribeCore(IHttpService service, HttpMessagePipe pipe)
        {
            //no direct subscriptions
        }

        public void GetAllBeforeFeed(HttpEntityManager entity, TFPos position, int count, EmbedLevel embed, bool headOfTf)
        {
            var envelope = new SendToHttpEnvelope(_networkSendQueue,
                                                  entity,
                                                  (args, msg) => Format.Atom.ReadAllEventsBackwardCompleted(args, msg, embed), 
                                                  (args, msg) => Configure.ReadAllEventsBackwardCompleted(args, msg, headOfTf));
            Publish(new ClientMessage.ReadAllEventsBackward(Guid.NewGuid(),
                                                            envelope,
                                                            position.CommitPosition,
                                                            position.PreparePosition,
                                                            count,
                                                            resolveLinks: true,
                                                            validationTfEofPosition: GetValidationTfEofPosition(entity, headOfTf)));
        }

        public void GetAllAfterFeed(HttpEntityManager entity, TFPos position, int count, EmbedLevel embed, bool headOfTf)
        {
            var envelope = new SendToHttpEnvelope(_networkSendQueue,
                                                  entity,
                                                  (args, msg) => Format.Atom.ReadAllEventsForwardCompleted(args, msg, embed),
                                                  (args, msg) => Configure.ReadAllEventsForwardCompleted(args, msg, headOfTf));
            Publish(new ClientMessage.ReadAllEventsForward(Guid.NewGuid(),
                                                           envelope,
                                                           position.CommitPosition,
                                                           position.PreparePosition,
                                                           count,
                                                           resolveLinks: true,
                                                           validationTfEofPosition: GetValidationTfEofPosition(entity, headOfTf)));
        }

        private static long? GetValidationTfEofPosition(HttpEntityManager entity, bool headOfTf)
        {
            if (headOfTf)
                return null;
            long tfEofPosition;
            var etag = entity.HttpEntity.Request.Headers["If-None-Match"];
            return etag.IsNotEmptyString() && long.TryParse(etag.Trim('\"'), out tfEofPosition) ? (long?) tfEofPosition : null;
        }
    }
}