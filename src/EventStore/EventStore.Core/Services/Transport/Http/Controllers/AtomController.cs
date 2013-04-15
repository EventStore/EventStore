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
using EventStore.Transport.Http;
using EventStore.Transport.Http.Atom;
using EventStore.Transport.Http.Codecs;
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
        private static readonly char[] ETagSeparator = new[] { ';' };

        private static readonly HtmlFeedCodec HtmlFeedCodec = new HtmlFeedCodec(); // initialization order matters
        private static readonly ICodec EventStoreJsonCodec = Codec.CreateCustom(Codec.Json, ContentType.AtomJson, Encoding.UTF8);

        private static readonly ICodec[] AtomCodecs = new[]
                                                      {
                                                          EventStoreJsonCodec,
                                                          Codec.Xml,
                                                          Codec.ApplicationXml,
                                                          Codec.CreateCustom(Codec.Xml, ContentType.Atom, Encoding.UTF8),
                                                          Codec.Json
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
        private static readonly ICodec[] EventDataSupportedCodecs = new ICodec[] { Codec.Json, Codec.Xml, Codec.ApplicationXml, Codec.Text };

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
            //  /streams/{stream}
            //  /streams/{stream}/head
            //  /streams/{stream}/head/data
            //  /streams/{stream}/head/metadata
            //  /streams/{stream}/head/{count}
            //  /streams/{stream}/head/backward/{count}
            //  /streams/{stream}/head/forward/{count}

            //  /streams/{stream}/{eventNumber}
            //  /streams/{stream}/{eventNumber}/data
            //  /streams/{stream}/{eventNumber}/metadata
            //  /streams/{stream}/{eventNumber}/{count}
            //  /streams/{stream}/{eventNumber}/backward/{count}
            //  /streams/{stream}/{eventNumber}/forward/{count}

            //  /streams/{stream}/metadata
            //  /streams/{stream}/metadata/data
            //  /streams/{stream}/metadata/metadata
            //  /streams/{stream}/metadata/{count}
            //  /streams/{stream}/metadata/backward/{count}  -- not supported
            //  /streams/{stream}/metadata/forward/{count}   -- not supported

            //  /streams/$all
            //  /streams/$all/head
            //  /streams/$all/head/data                 -- not supported
            //  /streams/$all/head/metadata             -- not supported
            //  /streams/$all/head/{count}
            //  /streams/$all/head/backward/{count}
            //  /streams/$all/head/forward/{count}

            //  /streams/$all/{position}
            //  /streams/$all/{position}/data           -- not supported
            //  /streams/$all/{position}/metadata       -- not supported
            //  /streams/$all/{position}/{count}
            //  /streams/$all/{position}/backward/{count}
            //  /streams/$all/{position}/forward/{count}

            // latest stream events (head of stream)
            Register(http, "/streams/{stream}?embed={embed}&count={count}", HttpMethod.Get, OnGetStreamHead, Codec.NoCodecs, AtomWithHtmlCodecs);
            Register(http, "/streams/{stream}/head?embed={embed}", HttpMethod.Get, OnGetStreamHead, Codec.NoCodecs, AtomWithHtmlCodecs);
            Register(http, "/streams/{stream}/head/{count}?embed={embed}", HttpMethod.Get, OnGetStreamHead, Codec.NoCodecs, AtomWithHtmlCodecs);

            // stream events range
            Register(http, "/streams/{stream}/range/{start}/{count}?embed={embed}", HttpMethod.Get, OnGetStreamRange, Codec.NoCodecs, AtomWithHtmlCodecs);

            // append event(s)
            Register(http, "/streams/{stream}", HttpMethod.Post, OnPostEntry, AtomCodecs, AtomCodecs);

            // delete stream
            Register(http, "/streams/{stream}", HttpMethod.Delete, OnDeleteStream, AtomCodecs, AtomCodecs);

            // get event entry
            Register(http, "/streams/{stream}/{eventNumber}?embed={embed}", HttpMethod.Get, OnGetEntry, Codec.NoCodecs, AtomWithHtmlCodecs);

            // get event data only
            Register(http, "/streams/{stream}/event/{version}?resolve={resolve}", HttpMethod.Get, OnGetEntryDataOnly, Codec.NoCodecs, EventDataSupportedCodecs);

            // get stream metadata
            Register(http, "/streams/{stream}/metadata?embed={embed}", HttpMethod.Get, OnGetMetadataEntry, Codec.NoCodecs, AtomWithHtmlCodecs);
            // set stream metadata
            Register(http, "/streams/{stream}/metadata", HttpMethod.Post, OnPostMetadataEntry, AtomCodecs, AtomCodecs);
            
            // read $all stream
            Register(http, "/streams/$all?embed={embed}", HttpMethod.Get, OnGetAllFeedBeforeHead, Codec.NoCodecs, AtomWithHtmlCodecs);
            Register(http, "/streams/$all/{count}?embed={embed}", HttpMethod.Get, OnGetAllFeedBeforeHead, Codec.NoCodecs, AtomWithHtmlCodecs);
            Register(http, "/streams/$all/before/{pos}/{count}?embed={embed}", HttpMethod.Get, OnGetAllFeedBefore, Codec.NoCodecs, AtomWithHtmlCodecs);
            Register(http, "/streams/$all/after/{pos}/{count}?embed={embed}", HttpMethod.Get, OnGetAllFeedAfter, Codec.NoCodecs, AtomWithHtmlCodecs);
        }

        //FEED
        private void OnGetStreamHead(HttpEntityManager manager, UriTemplateMatch match)
        {
            var stream = match.BoundVariables["stream"];
            var count = match.BoundVariables["count"];
            var embed = GetEmbedLevel(manager, match);

            int cnt = AtomSpecs.FeedPageSize;

            if (string.IsNullOrEmpty(stream))
            {
                SendBadRequest(manager, string.Format("Invalid stream name '{0}'", stream));
                return;
            }
            if (count.IsNotEmptyString() && (!int.TryParse(count, out cnt) || cnt <= 0))
            {
                SendBadRequest(manager, string.Format("'{0}' is not valid count. Should be positive integer", count));
                return;
            }

            _genericController.GetStreamFeedPage(manager, stream, -1, cnt, embed, GetETagStreamVersion(manager), headOfStream: true);
        }

        private void OnGetStreamRange(HttpEntityManager manager, UriTemplateMatch match)
        {
            var stream = match.BoundVariables["stream"];
            var start = match.BoundVariables["start"];
            var count = match.BoundVariables["count"];
            var embed = GetEmbedLevel(manager, match);
            
            int startIdx;
            int cnt;

            if (string.IsNullOrEmpty(stream))
            {
                SendBadRequest(manager, string.Format("Invalid stream name '{0}'", stream));
                return;
            }
            if (!int.TryParse(start, out startIdx) || startIdx < -1)
            {
                SendBadRequest(manager, string.Format("'{0}' is not valid start index", start));
                return;
            }
            if (!int.TryParse(count, out cnt) || cnt <= 0)
            {
                SendBadRequest(manager, string.Format("'{0}' is not valid count. Should be positive integer", count));
                return;
            }
            _genericController.GetStreamFeedPage(manager, stream, startIdx, cnt, embed,
                                                 GetETagStreamVersion(manager), headOfStream: startIdx == -1);
        }

        private int? GetETagStreamVersion(HttpEntityManager manager)
        {
            var etag = manager.HttpEntity.Request.Headers["If-None-Match"];
            if (etag.IsNotEmptyString())
            {
                // etag format is version;contenttypehash
                var splitted = etag.Trim('\"').Split(ETagSeparator);
                if (splitted.Length == 2)
                {
                    var typeHash = manager.ResponseCodec.ContentType.GetHashCode().ToString(CultureInfo.InvariantCulture);
                    int streamVersion;
                    return splitted[1] == typeHash && int.TryParse(splitted[0], out streamVersion) ? (int?) streamVersion : null;
                }
            }
            return null;
        }

        private static EmbedLevel GetEmbedLevel(HttpEntityManager manager, UriTemplateMatch match, EmbedLevel htmlLevel = EmbedLevel.PrettyBody)
        {
            if (manager.ResponseCodec is IRichAtomCodec)
                return htmlLevel;
            var rawValue = match.BoundVariables["embed"] ?? string.Empty;
            switch (rawValue.ToLowerInvariant())
            {
                case "rich": return EmbedLevel.Rich;
                case "body": return EmbedLevel.Body;
                case "pretty": return EmbedLevel.PrettyBody;
                case "tryharder": return EmbedLevel.TryHarder;
                default: return EmbedLevel.None;
            }
        }

        private void OnPostEntry(HttpEntityManager manager, UriTemplateMatch match)
        {
            var stream = match.BoundVariables["stream"];
            if (string.IsNullOrEmpty(stream))
            {
                SendBadRequest(manager, string.Format("Invalid request. Stream must be non-empty string"));
                return;
            }

            _genericController.PostEntry(manager, stream);
        }

        private void OnDeleteStream(HttpEntityManager manager, UriTemplateMatch match)
        {
            var stream = match.BoundVariables["stream"];
            if (string.IsNullOrEmpty(stream))
            {
                SendBadRequest(manager, string.Format("Invalid stream name '{0}'", stream));
                return;
            }

            _genericController.DeleteStream(manager, stream);
        }

        private void OnGetEntry(HttpEntityManager manager, UriTemplateMatch match)
        {
            var stream = match.BoundVariables["stream"];
            var evNum = match.BoundVariables["eventNumber"];
            var embed = GetEmbedLevel(manager, match, htmlLevel: EmbedLevel.TryHarder);
            int eventNumber;
            if (stream.IsEmptyString() || !int.TryParse(evNum, out eventNumber))
            {
                SendBadRequest(manager, "Stream must be non-empty string and id must be integer value");
                return;
            }

            _genericController.GetEntry(manager, stream, eventNumber, embed);
        }

        private void OnGetEntryDataOnly(HttpEntityManager manager, UriTemplateMatch match)
        {
            var stream = match.BoundVariables["stream"];
            var versionString = match.BoundVariables["version"];
            var resolve = match.BoundVariables["resolve"] ?? "yes";  //TODO: reply invalid ??? if neither NO nor YES
            
            int version;
            bool shouldResolve = resolve.Equals("yes", StringComparison.OrdinalIgnoreCase);

            if (stream.IsEmptyString() || !int.TryParse(versionString, out version))
            {
                SendBadRequest(manager, "Stream must bu non-empty string and id must be integer value");
                return;
            }

            var envelope = new SendToHttpEnvelope(_networkSendQueue,
                                                  manager,
                                                  Format.ReadEventCompleted,
                                                  Configure.ReadEventCompleted);
            Publish(new ClientMessage.ReadEvent(Guid.NewGuid(), envelope, stream, version, shouldResolve));
        }

        private void OnGetMetadataEntry(HttpEntityManager manager, UriTemplateMatch match)
        {
            var stream = match.BoundVariables["stream"];
            var embed = GetEmbedLevel(manager, match, htmlLevel: EmbedLevel.TryHarder);
            if (string.IsNullOrEmpty(stream) || SystemStreams.IsMetastream(stream))
            {
                SendBadRequest(manager, "Stream must be non-empty string and should not be metastream");
                return;
            }

            _genericController.GetEntry(manager, SystemStreams.MetastreamOf(stream), -1, embed);
        }

        private void OnPostMetadataEntry(HttpEntityManager manager, UriTemplateMatch match)
        {
            var stream = match.BoundVariables["stream"];
            if (string.IsNullOrEmpty(stream) || SystemStreams.IsMetastream(stream))
            {
                SendBadRequest(manager, string.Format("Invalid request. Stream must be non-empty string and should not be metastream"));
                return;
            }

            _genericController.PostEntry(manager, SystemStreams.MetastreamOf(stream));
        }

        //$ALL

        private void OnGetAllFeedBeforeHead(HttpEntityManager manager, UriTemplateMatch match)
        {
            var c = match.BoundVariables["count"];
            var embed = GetEmbedLevel(manager, match);

            int count = AtomSpecs.FeedPageSize;

            if (c.IsNotEmptyString() && !int.TryParse(c, out count))
            {
                SendBadRequest(manager, string.Format("Invalid count argument : {0}", c));
                return;
            }

            _allEventsController.GetAllBeforeFeed(manager, TFPos.Invalid, count, embed, headOfTf: true);
        }

        private void OnGetAllFeedBefore(HttpEntityManager manager, UriTemplateMatch match)
        {
            var p = match.BoundVariables["pos"];
            var c = match.BoundVariables["count"];
            var embed = GetEmbedLevel(manager, match);

            TFPos position;
            int count;

            if (!TFPos.TryParse(p, out position))
            {
                SendBadRequest(manager, string.Format("Invalid position argument : {0}", p));
                return;
            }
            if (!int.TryParse(c, out count))
            {
                SendBadRequest(manager, string.Format("Invalid count argument : {0}", c));
                return;
            }

            _allEventsController.GetAllBeforeFeed(manager, position, count, embed, headOfTf: false);
        }

        private void OnGetAllFeedAfter(HttpEntityManager manager, UriTemplateMatch match)
        {
            var p = match.BoundVariables["pos"];
            var c = match.BoundVariables["count"];
            var embed = GetEmbedLevel(manager, match);

            TFPos position;
            int count;

            if (!TFPos.TryParse(p, out position))
            {
                SendBadRequest(manager, string.Format("Invalid position argument : {0}", p));
                return;
            }
            if (!int.TryParse(c, out count))
            {
                SendBadRequest(manager, string.Format("Invalid count argument : {0}", c));
                return;
            }

            _allEventsController.GetAllAfterFeed(manager, position, count, embed, headOfTf: false);
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

        public void DeleteStream(HttpEntityManager manager, string stream)
        {
            manager.AsyncState = stream;
            manager.ReadTextRequestAsync(DeleteStreamBodyRead,
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

        public void GetStreamFeedPage(HttpEntityManager manager, string stream, int start, int count, EmbedLevel embed, 
                                      int? validationStreamVersion, bool headOfStream)
        {
            manager.AsyncState = start;
            var envelope = new SendToHttpEnvelope(
                _networkSendQueue,
                manager,
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

        public void GetEntry(HttpEntityManager manager, string stream, int eventNumber, EmbedLevel embed)
        {
            var envelope = new SendToHttpEnvelope(_networkSendQueue, 
                                                  manager,
                                                  (args, message) => Format.Atom.ReadEventCompletedEntry(args, message, embed), 
                                                  Configure.ReadEventCompleted);
            Publish(new ClientMessage.ReadEvent(Guid.NewGuid(), envelope, stream, eventNumber, true));
        }

        public void PostEntry(HttpEntityManager manager, string stream)
        {
            manager.AsyncState = stream;
            manager.ReadTextRequestAsync(OnPostEntryRequestRead, 
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

        public void GetAllBeforeFeed(HttpEntityManager manager, TFPos position, int count, EmbedLevel embed, bool headOfTf)
        {
            var envelope = new SendToHttpEnvelope(_networkSendQueue,
                                                  manager,
                                                  (args, msg) => Format.Atom.ReadAllEventsBackwardCompleted(args, msg, embed), 
                                                  (args, msg) => Configure.ReadAllEventsBackwardCompleted(args, msg, headOfTf));
            Publish(new ClientMessage.ReadAllEventsBackward(Guid.NewGuid(),
                                                            envelope,
                                                            position.CommitPosition,
                                                            position.PreparePosition,
                                                            count,
                                                            resolveLinks: true,
                                                            validationTfEofPosition: GetValidationTfEofPosition(manager, headOfTf)));
        }

        public void GetAllAfterFeed(HttpEntityManager manager, TFPos position, int count, EmbedLevel embed, bool headOfTf)
        {
            var envelope = new SendToHttpEnvelope(_networkSendQueue,
                                                  manager,
                                                  (args, msg) => Format.Atom.ReadAllEventsForwardCompleted(args, msg, embed),
                                                  (args, msg) => Configure.ReadAllEventsForwardCompleted(args, msg, headOfTf));
            Publish(new ClientMessage.ReadAllEventsForward(Guid.NewGuid(),
                                                           envelope,
                                                           position.CommitPosition,
                                                           position.PreparePosition,
                                                           count,
                                                           resolveLinks: true,
                                                           validationTfEofPosition: GetValidationTfEofPosition(manager, headOfTf)));
        }

        private static long? GetValidationTfEofPosition(HttpEntityManager manager, bool headOfTf)
        {
            if (headOfTf)
                return null;
            long tfEofPosition;
            var etag = manager.HttpEntity.Request.Headers["If-None-Match"];
            return etag.IsNotEmptyString() && long.TryParse(etag.Trim('\"'), out tfEofPosition) ? (long?) tfEofPosition : null;
        }
    }
}